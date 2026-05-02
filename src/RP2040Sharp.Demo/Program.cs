using RP2040.TestKit;
using RP2040.TestKit.Boards;

namespace RP2040Sharp.Demo;

/// <summary>
/// RP2040Sharp Demo — boots MicroPython on the emulated Raspberry Pi Pico
/// and drives the REPL over the emulated UART to execute Python snippets.
///
/// Usage:
///   dotnet run --project src/RP2040Sharp.Demo
/// </summary>
internal static class Program
{
    private const string MicroPythonVersion = "v1.21.0";

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

        // Capture BKPT (panic halt) — report the return address so we know the caller
        string? panicInfo = null;
        pico.Cpu.OnBreakpoint = imm8 =>
        {
            var lr = pico.Cpu.Registers.LR & ~1u;
            var r0 = pico.Cpu.Registers.R0;
            var ipsr = pico.Cpu.Registers.IPSR;
            Console.Error.WriteLine($"  [bkpt] BKPT #{imm8}: PC=0x{pico.Cpu.Registers.PC:X8} LR=0x{lr:X8} R0=0x{r0:X8} IPSR={ipsr}");
            panicInfo ??= $"BKPT #{imm8}: panic called from LR=0x{lr:X8} R0=0x{r0:X8} IPSR={ipsr}";
        };
        // Hook into panic() itself to capture who called it
        pico.Cpu.RegisterNativeHook(0x1003054C, cpu =>
        {
            var callerLr = cpu.Registers.LR & ~1u;
            var msg = cpu.Registers.R0; // panic message pointer (may be null for simple panic)
            Console.Error.WriteLine($"  [panic] panic() called from LR=0x{callerLr:X8} R0=0x{msg:X8}");
            // Let execution continue into the real panic code by *not* doing anything here
            // (but the hook mechanism replaces with BX LR — so we must re-emit a fake "call")
        });
        // Hook into hard_assert wrapper to capture its caller
        pico.Cpu.RegisterNativeHook(0x1003057C, cpu =>
        {
            var callerLr = cpu.Registers.LR & ~1u;
            Console.Error.WriteLine($"  [hard_assert] called from LR=0x{callerLr:X8} R0=0x{cpu.Registers.R0:X8} R1=0x{cpu.Registers.R1:X8} R2=0x{cpu.Registers.R2:X8} R3=0x{cpu.Registers.R3:X8}");
        });
        // Trace exc#19 handler (0x1002DDB8) first 5 times
        int excHandlerCount = 0;
        pico.Cpu.RegisterNativeHook(0x1002DDB8, cpu =>
        {
            if (++excHandlerCount <= 5)
                Console.Error.WriteLine($"  [exc19-handler] exc#19 entry: LR=0x{cpu.Registers.LR:X8} SP=0x{cpu.Registers.SP:X8} R0=0x{cpu.Registers.R0:X8} R1=0x{cpu.Registers.R1:X8} cycles={cpu.Cycles}");
        });

        Console.Write("[USB-CDC] Tracing boot...");
        Console.Error.WriteLine($"\n  [trace] Initial PC=0x{pico.Cpu.Registers.PC:X8} SP=0x{pico.Cpu.Registers.SP:X8}");

        // Run in batches until USB-CDC enumerates and produces output, or BootROM is entered
        var bootRomHit = false;
        var prevCycles = pico.Cpu.Cycles;
        for (var ms = 0; ms < 20_000 && pico.UsbCdc.ByteCount == 0; ms += 100)
        {
            try { pico.RunMilliseconds(100); }
            catch (Exception ex) {
                Console.Error.WriteLine($"  [crash] Exception at ms={ms+100}: {ex.GetType().Name}: {ex.Message}");
                Console.Error.WriteLine($"  [crash] PC=0x{pico.Cpu.Registers.PC:X8} SP=0x{pico.Cpu.Registers.SP:X8} LR=0x{pico.Cpu.Registers.LR:X8}");
                Console.Error.WriteLine($"  [crash] R0=0x{pico.Cpu.Registers.R0:X8} R1=0x{pico.Cpu.Registers.R1:X8} CDC={pico.UsbCdc.ByteCount}B UART={pico.Uart0.ByteCount}B");
                break;
            }
            var pc = pico.Cpu.Registers.PC;
            if ((pc >> 28) == 0 && pc > 8 && !bootRomHit)
            {
                Console.Error.WriteLine($"  [trace] BootROM entered at PC=0x{pc:X8} after {ms+100}ms sim");
                bootRomHit = true;
            }
            if (pico.UsbCdc.ByteCount > 0)
                Console.Error.WriteLine($"  [trace] First CDC byte at {ms+100}ms sim (enumerated={pico.UsbCdc.IsConnected})");
            // Print PC snapshot every simulated second to track progress
            if ((ms % 1000) == 900)
                Console.Error.WriteLine($"  [trace] {ms+100}ms: PC=0x{pc:X8} CDC={pico.UsbCdc.ByteCount}B (enum={pico.UsbCdc.IsConnected}) UART={pico.Uart0.ByteCount}B");
        }

        var booted = pico.RunUntilOutput(pico.UsbCdc, ">>> ", timeoutMs: 60_000);
        if (!booted)
        {
            // Print last 10 seconds snapshot
            for (var s = 0; s < 10; s++)
            {
                pico.RunMilliseconds(1000);
                Console.Error.WriteLine($"  [snap] PC=0x{pico.Cpu.Registers.PC:X8} CDC={pico.UsbCdc.ByteCount}B text='{pico.UsbCdc.Text.Replace("\r","\\r").Replace("\n","\\n")[..Math.Min(100, pico.UsbCdc.Text.Length)]}'");
            }
            Console.WriteLine();
            Console.Error.WriteLine($"  [debug] CDC captured {pico.UsbCdc.ByteCount} bytes hex: {BitConverter.ToString(pico.UsbCdc.Bytes.Take(64).ToArray())}");
            Console.Error.WriteLine($"  [debug] CDC text: '{pico.UsbCdc.Text.Replace("\r", "\\r").Replace("\n", "\\n")[..Math.Min(500, pico.UsbCdc.Text.Length)]}'");
            Console.Error.WriteLine($"  [debug] UART captured {pico.Uart0.ByteCount} bytes");
            Console.Error.WriteLine($"  [debug] CPU cycles executed: {pico.Cpu.Cycles:N0}");
            Console.Error.WriteLine($"  [debug] CDC enumerated: {pico.UsbCdc.IsConnected}");
            if (panicInfo is not null)
                Console.Error.WriteLine($"  [debug] {panicInfo}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: MicroPython did not produce a REPL prompt within 60 s.");
            Console.ResetColor();
            return 1;
        }
        Console.WriteLine(" ready!");
        Console.WriteLine(new string('─', 60));
        Console.WriteLine();

        // ── 3. REPL demo ──────────────────────────────────────────────────────
        RunDemo(pico, "1 + 1",                           expectedOutput: "2");
        RunDemo(pico, "2 ** 10",                         expectedOutput: "1024");
        RunDemo(pico, "import sys; print(sys.platform)", expectedOutput: "rp2");
        RunDemo(pico, "print('Hello from RP2040Sharp!')", expectedOutput: "Hello from RP2040Sharp!");
        RunDemo(pico, "import machine; print(machine.freq())", expectedOutput: null);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Demo complete.");
        Console.ResetColor();
        return 0;
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
            // Just wait for next prompt
            pico.RunUntilOutput(pico.UsbCdc, ">>> ", timeoutMs: 5_000);
        }

        // Print whatever was emitted on CDC since the command was injected
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

            // Resolve the exact filename (includes build date) from the download page
            var url = await ResolveMicroPythonUrlAsync(http, version);
            if (url is null) return null;

            var bytes = await http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [debug] {ex.GetType().Name}: {ex.Message}");
            if (File.Exists(path)) File.Delete(path);
            return null;
        }
    }

    private static async Task<string?> ResolveMicroPythonUrlAsync(HttpClient http, string version)
    {
        // Firmware is listed at https://micropython.org/download/RPI_PICO/
        // Each entry looks like: /resources/firmware/RPI_PICO-{date}-{version}.uf2
        var page = await http.GetStringAsync("https://micropython.org/download/RPI_PICO/");
        // Filenames are RPI_PICO-{date}-v{semver}.uf2 — keep the v prefix
        var tag = version.StartsWith('v') ? version : "v" + version;
        const string needle = "/resources/firmware/RPI_PICO-";
        var search = $"-{tag}.uf2";  // e.g. "-v1.23.0.uf2" (no trailing quote — rel slice excludes it)

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
