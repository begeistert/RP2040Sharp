using System.Diagnostics;
using RP2040.Peripherals;
using RP2040.TestKit;
using RP2040.TestKit.Boards;

namespace RP2040Sharp.Demo;

/// <summary>
/// RP2040Sharp Demo — boots MicroPython on the emulated Raspberry Pi Pico and
/// exposes an interactive REPL over the emulated USB-CDC connection.
///
/// Usage:
///   dotnet run --project src/RP2040Sharp.Demo
///
/// Type any Python expression at the prompt and press Enter. Press Ctrl+C or
/// pipe EOF to exit.
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
            Console.Error.WriteLine("FAILED (network unavailable or release not found)");
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

        // Forward CDC output to the console immediately as bytes arrive.
        pico.UsbCdcHost.OnSerialData += data =>
        {
            var text = System.Text.Encoding.Latin1.GetString(data);
            Console.Write(text);
        };

        var wallClock = Stopwatch.StartNew();

        // Wait for MicroPython to produce the first REPL prompt.
        var booted = pico.RunUntilOutput(pico.UsbCdc, ">>> ", timeoutMs: 60_000);
        if (!booted)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("\nERROR: MicroPython did not produce a REPL prompt within 60 s.");
            Console.ResetColor();
            return 1;
        }

        var bootMs = wallClock.Elapsed.TotalMilliseconds;
        var bootSimMs = pico.Cpu.Cycles / (RP2040_CLK_HZ / 1_000.0);
        Console.WriteLine(new string('─', 60));
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"MicroPython ready!  ({FormatTime(bootMs)} wall · {bootSimMs / 1000.0:F2} s simulated)");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Interactive MicroPython REPL — type Python and press Enter.  Ctrl+C to exit.");
        Console.WriteLine(new string('─', 60));

        // ── 3. Interactive REPL loop ──────────────────────────────────────────
        // Use a CancellationToken so Ctrl+C cleanly stops both tasks.
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        // Sim task: advance the emulated CPU continuously in small slices so the
        // MicroPython interpreter keeps running while we wait for console input.
        var simTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    pico.RunMilliseconds(10);
                    // Yield to the I/O task between sim slices.
                    await Task.Yield();
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"\n[sim crash] {ex.GetType().Name}: {ex.Message}");
                    cts.Cancel();
                    break;
                }
            }
        }, cts.Token);

        // I/O task: read lines from stdin and forward them to the MicroPython REPL.
        var ioTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                // ReadLineAsync does not accept a CancellationToken in all runtimes,
                // so we poll cancellation after each line.
                string? line;
                try { line = await Console.In.ReadLineAsync(cts.Token); }
                catch (OperationCanceledException) { break; }

                if (line is null) { cts.Cancel(); break; }  // EOF
                pico.UsbCdc.InjectString(line + "\r\n");
            }
        }, cts.Token);

        // Wait for either task to finish (Ctrl+C, EOF, or crash).
        await Task.WhenAny(simTask, ioTask);
        cts.Cancel();
        try { await Task.WhenAll(simTask, ioTask); } catch { /* ignore cancellation */ }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("REPL session ended.");
        Console.ResetColor();
        return 0;
    }

    private static string FormatTime(double ms) =>
        ms < 1000 ? $"{ms:F0} ms" : $"{ms / 1000.0:F2} s";

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║    RP2040Sharp Demo — Interactive MicroPython REPL       ║");
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
}
