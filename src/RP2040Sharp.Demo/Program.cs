using System.Diagnostics;
using RP2040.Peripherals;
using RP2040.TestKit;
using RP2040.TestKit.Boards;

namespace RP2040Sharp.Demo;

/// <summary>
/// RP2040Sharp Demo — boots MicroPython on the emulated Raspberry Pi Pico
/// and drives the REPL over the emulated USB-CDC to execute Python snippets.
///
/// Usage:
///   dotnet run --project src/RP2040Sharp.Demo
/// </summary>
internal static class Program
{
    private const string MicroPythonVersion = "v1.21.0";
    private const double RP2040_CLK_HZ = 125_000_000.0;

    private static async Task<int> Main()
    {
        PrintBanner();

        // ── 1. Firmware ───────────────────────────────────────────────────────
        Console.Write($"Downloading MicroPython {MicroPythonVersion}... ");
        var uf2Path = await DownloadFirmwareAsync(MicroPythonVersion);
        if (uf2Path is null)
        {
            Console.WriteLine("FAILED (network unavailable or release not found)");
            return 1;
        }
        Console.WriteLine("OK");

        Console.Write("Parsing UF2... ");
        var flash = RP2040Machine.Uf2ToFlash(await File.ReadAllBytesAsync(uf2Path))
            ?? throw new InvalidDataException("Not a valid UF2 file.");
        Console.WriteLine($"OK ({flash.Length / 1024} KB)");

        // ── 2. Boot ───────────────────────────────────────────────────────────
        Console.WriteLine();
        Console.WriteLine("Booting on emulated Raspberry Pi Pico...");
        Console.WriteLine(new string('─', 60));

        using var pico = new PicoSimulation();
        pico.LoadFlash(flash);

        string? panicInfo = null;
        pico.Cpu.OnBreakpoint = imm8 =>
        {
            var lr = pico.Cpu.Registers.LR & ~1u;
            panicInfo ??= $"BKPT #{imm8}: panic at LR=0x{lr:X8} R0=0x{pico.Cpu.Registers.R0:X8}";
        };

        var wallClock = Stopwatch.StartNew();

        Console.Write("[USB-CDC] Waiting for MicroPython REPL...");
        for (var ms = 0; ms < 20_000 && pico.UsbCdc.ByteCount == 0; ms += 100)
        {
            try { pico.RunMilliseconds(100); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"\nCrash at sim {ms + 100}ms: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine($"PC=0x{pico.Cpu.Registers.PC:X8}  LR=0x{pico.Cpu.Registers.LR:X8}");
                break;
            }
        }

        var booted = pico.RunUntilOutput(pico.UsbCdc, ">>> ", timeoutMs: 60_000);
        if (!booted)
        {
            var text = pico.UsbCdc.Text.Replace("\r", "\\r").Replace("\n", "\\n");
            Console.Error.WriteLine($"\nCDC output: '{text[..Math.Min(500, text.Length)]}'");
            if (panicInfo is not null) Console.Error.WriteLine(panicInfo);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nERROR: MicroPython did not produce a REPL prompt within 60 s.");
            Console.ResetColor();
            return 1;
        }

        var bootCycles = pico.Cpu.Cycles;
        var bootWallMs = wallClock.Elapsed.TotalMilliseconds;
        var bootSimMs  = bootCycles / (RP2040_CLK_HZ / 1000.0);
        Console.WriteLine($" ready!  ({FormatTime(bootWallMs)} wall · {bootSimMs / 1000.0:F2} s simulated)");
        Console.WriteLine(new string('─', 60));
        Console.WriteLine();

        // ── 3. REPL demo ──────────────────────────────────────────────────────
        var replStartCycles = pico.Cpu.Cycles;
        var replStartWall   = wallClock.Elapsed;
        RunDemo(pico, "1 + 1",                               expectedOutput: "2");
        RunDemo(pico, "2 ** 10",                             expectedOutput: "1024");
        RunDemo(pico, "import sys; print(sys.platform)",     expectedOutput: "rp2");
        RunDemo(pico, "print('Hello from RP2040Sharp!')",    expectedOutput: "Hello from RP2040Sharp!");
        RunDemo(pico, "import machine; print(machine.freq())", expectedOutput: null);

        // ── 4. Performance report ─────────────────────────────────────────────
        var replCycles = pico.Cpu.Cycles - replStartCycles;
        var replWallMs = (wallClock.Elapsed - replStartWall).TotalMilliseconds;
        wallClock.Stop();

        Console.WriteLine();
        Console.Write("Running micro-benchmark (tight Flash loop)...");
        var (benchMin, benchMax, benchAvg) = RunMicroBenchmark();
        Console.WriteLine($" done  ({benchAvg:F0} MIPS avg)");

        PrintPerformance(bootCycles, bootWallMs, replCycles, replWallMs, benchMin, benchMax, benchAvg);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Demo complete.");
        Console.ResetColor();
        return 0;
    }

    private static string FormatTime(double ms) =>
        ms < 1000 ? $"{ms:F0} ms" : $"{ms / 1000.0:F2} s";

    private static void PrintPerformance(
        long bootCycles, double bootWallMs,
        long replCycles, double replWallMs,
        double benchMin, double benchMax, double benchAvg)
    {
        var bootMips     = bootCycles / 1_000_000.0 / (bootWallMs / 1000.0);
        var replMips     = replCycles > 0 ? replCycles / 1_000_000.0 / (replWallMs / 1000.0) : 0;
        var totalCycles  = bootCycles + replCycles;
        var totalWallMs  = bootWallMs + replWallMs;
        var totalWallSec = totalWallMs / 1000.0;
        var simSecs      = totalCycles / RP2040_CLK_HZ;
        var totalMips    = totalCycles / 1_000_000.0 / totalWallSec;
        var speedup      = simSecs / totalWallSec;

        Console.WriteLine(new string('─', 60));
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  Phase breakdown:");
        Console.WriteLine($"    Boot : {bootCycles / 1_000_000.0,6:F0} M cycles  {FormatTime(bootWallMs),-10}  {bootMips,6:F0} MIPS");
        Console.WriteLine($"    REPL : {replCycles / 1_000_000.0,6:F0} M cycles  {FormatTime(replWallMs),-10}  {replMips,6:F0} MIPS");
        Console.WriteLine();
        Console.WriteLine($"  Total simulated : {totalCycles / 1_000_000.0:F0} M cycles  /  {simSecs:F2} s emulated");
        Console.WriteLine($"  Total wall time : {FormatTime(totalWallMs)}");
        Console.WriteLine($"  Overall MIPS    : {totalMips:F0}");
        Console.WriteLine($"  Speed vs RP2040 : {speedup:F3}×  (real hardware @ 125 MHz)");
        Console.WriteLine();
        Console.WriteLine("  Micro-benchmark (tight arithmetic loop, Flash):");
        Console.WriteLine($"    min {benchMin:F0}  ·  avg {benchAvg:F0}  ·  max {benchMax:F0} MIPS");
        Console.ResetColor();
        Console.WriteLine(new string('─', 60));
        Console.WriteLine();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void RunDemo(PicoSimulation pico, string pythonCode, string? expectedOutput)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write(">>> ");
        Console.ResetColor();
        Console.WriteLine(pythonCode);

        pico.UsbCdc.Clear();
        pico.UsbCdc.InjectString(pythonCode + "\r\n");

        if (expectedOutput is not null)
        {
            var found = pico.RunUntilOutput(pico.UsbCdc, expectedOutput, timeoutMs: 5_000);
            if (!found)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [warn] expected '{expectedOutput}' within 5 s");
                Console.ResetColor();
            }
        }
        else
        {
            pico.RunUntilOutput(pico.UsbCdc, ">>> ", timeoutMs: 5_000);
        }

        var output = pico.UsbCdc.Text.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0 && l != pythonCode && l != ">>> ")
            .FirstOrDefault();
        if (output is not null)
            Console.WriteLine(output);
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          RP2040Sharp Demo — MicroPython on emulated Pico ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    // ── Firmware download ─────────────────────────────────────────────────────

    private static readonly string CacheDir =
        Path.Combine(Path.GetTempPath(), "rp2040sharp-firmware-cache");

    private static async Task<string?> DownloadFirmwareAsync(string version)
    {
        Directory.CreateDirectory(CacheDir);
        var path = Path.Combine(CacheDir, $"micropython-{version}.uf2");

        if (File.Exists(path) && new FileInfo(path).Length > 0)
            return path;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("RP2040Sharp-Demo/1.0");

            var url = await ResolveMicroPythonUrlAsync(http, version);
            if (url is null) return null;

            var bytes = await http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Download failed: {ex.GetType().Name}: {ex.Message}");
            if (File.Exists(path)) File.Delete(path);
            return null;
        }
    }

    private static async Task<string?> ResolveMicroPythonUrlAsync(HttpClient http, string version)
    {
        // Firmware is listed at https://micropython.org/download/RPI_PICO/
        // Each entry looks like: /resources/firmware/RPI_PICO-{date}-{version}.uf2
        var page = await http.GetStringAsync("https://micropython.org/download/RPI_PICO/");
        var tag = version.StartsWith('v') ? version : "v" + version;
        const string needle = "/resources/firmware/RPI_PICO-";
        var search = $"-{tag}.uf2";

        var start = page.IndexOf(needle, StringComparison.Ordinal);
        while (start >= 0)
        {
            var end = page.IndexOf('"', start + 1);
            if (end < 0) break;
            var rel = page[start..end];
            if (rel.EndsWith(search, StringComparison.OrdinalIgnoreCase))
                return "https://micropython.org" + rel;
            start = page.IndexOf(needle, start + 1, StringComparison.Ordinal);
        }
        return null;
    }

    private static (double min, double max, double avg) RunMicroBenchmark()
    {
        const int Rounds = 5;
        const int InstructionsPerRound = 10_000_000;

        // Tight loop: movs r0,#0 | movs r1,#1 | 4×adds r0,r0,r1 | b -14
        // No stack, no I/O — pure instruction dispatch throughput.
        // PC starts at 0x10000000 (LoadFlash forces this), branch target 0x10000002.
        ReadOnlySpan<byte> code = [
            0x00, 0x20,  // movs r0, #0
            0x01, 0x21,  // movs r1, #1        ← branch target (0x10000002)
            0x40, 0x18, 0x40, 0x18,             // adds r0, r0, r1 (×2)
            0x40, 0x18, 0x40, 0x18,             // adds r0, r0, r1 (×2)
            0xF9, 0xE7,  // b -14 → 0x10000002
        ];

        var firmware = new byte[16];
        code.CopyTo(firmware);

        using var machine = new RP2040Machine();
        machine.LoadFlash(firmware);

        var results = new double[Rounds];
        for (var i = 0; i < Rounds; i++)
        {
            var sw = Stopwatch.StartNew();
            machine.Cpu.Run(InstructionsPerRound);
            sw.Stop();
            results[i] = InstructionsPerRound / 1_000_000.0 / sw.Elapsed.TotalSeconds;
        }

        return (results.Min(), results.Max(), results.Average());
    }
}
