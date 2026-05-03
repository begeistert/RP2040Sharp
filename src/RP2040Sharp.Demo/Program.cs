using System.Diagnostics;
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
        var flash = Uf2ToFlash(await File.ReadAllBytesAsync(uf2Path));
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
        RunDemo(pico, "1 + 1",                               expectedOutput: "2");
        RunDemo(pico, "2 ** 10",                             expectedOutput: "1024");
        RunDemo(pico, "import sys; print(sys.platform)",     expectedOutput: "rp2");
        RunDemo(pico, "print('Hello from RP2040Sharp!')",    expectedOutput: "Hello from RP2040Sharp!");
        RunDemo(pico, "import machine; print(machine.freq())", expectedOutput: null);

        // ── 4. Performance report ─────────────────────────────────────────────
        wallClock.Stop();
        PrintPerformance(pico.Cpu.Cycles, wallClock.Elapsed);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Demo complete.");
        Console.ResetColor();
        return 0;
    }

    private static string FormatTime(double ms) =>
        ms < 1000 ? $"{ms:F0} ms" : $"{ms / 1000.0:F2} s";

    private static void PrintPerformance(long totalCycles, TimeSpan wallTime)
    {
        var wallMs    = wallTime.TotalMilliseconds;
        var wallSecs  = wallTime.TotalSeconds;
        var simSecs   = totalCycles / RP2040_CLK_HZ;
        var mips      = totalCycles / 1_000_000.0 / wallSecs;
        var speedup   = simSecs / wallSecs;

        Console.WriteLine();
        Console.WriteLine(new string('─', 60));
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  Simulated cycles : {totalCycles / 1_000_000.0:F0} M");
        Console.WriteLine($"  Wall time        : {FormatTime(wallMs)}");
        Console.WriteLine($"  Emulated time    : {simSecs:F2} s");
        Console.WriteLine($"  Throughput       : {mips:F0} MIPS");
        Console.WriteLine($"  Speed vs RP2040  : {speedup:F3}×  (real hardware @ 125 MHz)");
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

    // ── UF2 parser ────────────────────────────────────────────────────────────

    private const uint UF2MagicStart0 = 0x0A324655;
    private const uint UF2MagicStart1 = 0x9E5D5157;
    private const uint FlashBase      = 0x10000000;

    private static byte[] Uf2ToFlash(byte[] uf2)
    {
        var blocks = uf2.Length / 512;
        uint minAddr = uint.MaxValue, maxAddr = 0;

        for (var i = 0; i < blocks; i++)
        {
            var off = i * 512;
            if (ReadU32(uf2, off) != UF2MagicStart0 || ReadU32(uf2, off + 4) != UF2MagicStart1) continue;
            var addr = ReadU32(uf2, off + 12);
            var size = ReadU32(uf2, off + 16);
            if (size == 0 || size > 256) continue;
            if (addr < minAddr) minAddr = addr;
            if (addr + size > maxAddr) maxAddr = addr + size;
        }

        if (minAddr == uint.MaxValue) throw new InvalidDataException("No valid UF2 blocks.");
        if (minAddr < FlashBase) throw new InvalidDataException($"UF2 target 0x{minAddr:X8} below flash base.");

        var image = new byte[maxAddr - FlashBase];
        Array.Fill(image, (byte)0xFF);

        for (var i = 0; i < blocks; i++)
        {
            var off = i * 512;
            if (ReadU32(uf2, off) != UF2MagicStart0 || ReadU32(uf2, off + 4) != UF2MagicStart1) continue;
            var addr = ReadU32(uf2, off + 12);
            var size = ReadU32(uf2, off + 16);
            if (size == 0 || size > 256) continue;
            Buffer.BlockCopy(uf2, off + 32, image, (int)(addr - FlashBase), (int)size);
        }

        return image;
    }

    private static uint ReadU32(byte[] b, int o) =>
        (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
}
