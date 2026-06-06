using System.Diagnostics;
using RP2040.Peripherals;
using RP2040.TestKit;
using RP2040.TestKit.Boards;

namespace RP2040Sharp.Demo.CircuitPython.Blink;

/// <summary>
/// CircuitPython "blink" demo — boots CircuitPython on the emulated Raspberry Pi Pico,
/// pastes the Adafruit blink example into the REPL, and monitors GPIO 25 (board.LED)
/// while the script toggles it for 20+ seconds.
///
/// Usage:
///   dotnet run --project src/RP2040Sharp.Demo.CircuitPython.Blink
///
/// Output: a real-time, time-stamped log of every LED state change emitted by the
/// CircuitPython firmware, plus the final blink count and total simulated time.
/// </summary>
internal static class Program
{
    private const string CircuitPythonVersion = "9.2.1";
    private const double RP2040_CLK_HZ        = 125_000_000.0;
    private const int    LedPin               = 25;     // board.LED on the Raspberry Pi Pico
    private const double TargetRunSeconds     = 20.0;   // user requirement: ≥ 20 s of blinking
    private const double BlinkHalfPeriodSec   = 0.25;   // 250 ms on, 250 ms off ⇒ 2 Hz

    private static async Task<int> Main()
    {
        PrintBanner();

        // ── 1. Firmware ───────────────────────────────────────────────────────
        Console.Write($"Downloading CircuitPython {CircuitPythonVersion}... ");
        var uf2Path = await DownloadFirmwareAsync(CircuitPythonVersion);
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
        Console.WriteLine("Booting CircuitPython on emulated Raspberry Pi Pico...");
        Console.WriteLine(new string('─', 60));

        using var pico = new PicoSimulation();
        pico.LoadFlash(flash);

        // Stream any CDC chatter (banner, REPL output) to stdout, dimmed so it doesn't
        // compete visually with the GPIO event log we'll print below.
        pico.UsbCdcHost.OnSerialData += data =>
        {
            var text = System.Text.Encoding.Latin1.GetString(data);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(text);
            Console.ResetColor();
        };

        var wallClock = Stopwatch.StartNew();

        // CircuitPython prints a "Press any key to enter the REPL" banner when no
        // code.py exists — answer it once so the REPL prompt actually appears.
        var promptReached = WaitForRepl(pico, timeoutMs: 30_000);
        if (!promptReached)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("\nERROR: CircuitPython did not produce a REPL prompt within 30 s.");
            Console.ResetColor();
            return 1;
        }

        var bootMs    = wallClock.Elapsed.TotalMilliseconds;
        var bootSimMs = pico.Cpu.Cycles / (RP2040_CLK_HZ / 1_000.0);
        Console.WriteLine(new string('─', 60));
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"REPL ready!  ({FormatTime(bootMs)} wall · {bootSimMs / 1000.0:F2} s simulated)");
        Console.ResetColor();
        Console.WriteLine();

        // ── 3. Inject the Adafruit blink example ──────────────────────────────
        // Loop count is sized so the script blinks for at least TargetRunSeconds.
        // Each iteration toggles the LED on then off, taking 2*BlinkHalfPeriodSec.
        var iterations = (int)Math.Ceiling(TargetRunSeconds / (2.0 * BlinkHalfPeriodSec));
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Pasting blink program ({iterations} cycles, ~{2 * iterations * BlinkHalfPeriodSec:F1} s)...");
        Console.ResetColor();

        var script =
            "import board, digitalio, time\n" +
            "led = digitalio.DigitalInOut(board.LED)\n" +
            "led.direction = digitalio.Direction.OUTPUT\n" +
            $"for i in range({iterations}):\n" +
            "    led.value = True\n" +
            $"    time.sleep({BlinkHalfPeriodSec})\n" +
            "    led.value = False\n" +
            $"    time.sleep({BlinkHalfPeriodSec})\n" +
            "print('blink: done')\n";

        // CircuitPython's REPL paste mode (Ctrl-E … Ctrl-D) preserves the indentation
        // of the for-loop body, which a plain newline-separated injection would lose.
        pico.UsbCdc.InjectString("\x05");                       // Ctrl-E: enter paste mode
        pico.RunMilliseconds(50);
        pico.UsbCdc.InjectString(script);
        pico.UsbCdc.InjectString("\x04");                       // Ctrl-D: run pasted block

        // Drop everything captured so far (banner + paste-mode echo). After this point the
        // CDC buffer only contains real program output, so the early-exit sentinel won't
        // match the script's own source text.
        pico.RunMilliseconds(50);
        pico.UsbCdc.Clear();

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("GPIO 25 (board.LED) event log:");
        Console.ResetColor();
        Console.WriteLine(new string('─', 60));

        // ── 4. Run the simulation and log LED state changes ───────────────────
        // The loop is gated on SIMULATED time, not wall time, because the demo's
        // contract is "≥ 20 s of simulated blinking" regardless of host speed.
        var blinkSimMs0 = pico.Cpu.Cycles / (RP2040_CLK_HZ / 1_000.0);
        var maxSimMs    = (TargetRunSeconds + 3) * 1000.0;   // small grace window

        var lastLed = pico.Gpio[LedPin].DigitalValue;
        var transitions = 0;

        while (true)
        {
            pico.RunMilliseconds(20);

            var nowLed = pico.Gpio[LedPin].DigitalValue;
            if (nowLed != lastLed)
            {
                transitions++;
                lastLed = nowLed;
                var simS = (pico.Cpu.Cycles / (RP2040_CLK_HZ / 1_000.0) - blinkSimMs0) / 1000.0;
                Console.ForegroundColor = nowLed ? ConsoleColor.Green : ConsoleColor.DarkGray;
                Console.WriteLine($"  [t = {simS,6:F2} s]  LED {(nowLed ? "ON " : "OFF")}  ({transitions} transitions)");
                Console.ResetColor();
            }

            var simElapsedMs = pico.Cpu.Cycles / (RP2040_CLK_HZ / 1_000.0) - blinkSimMs0;

            // Stop when the script signals completion (now safe — buffer was cleared above)
            // or when we've simulated past the deadline, whichever happens first.
            if (pico.UsbCdc.Text.Contains("blink: done", StringComparison.Ordinal))
            {
                pico.RunMilliseconds(200);
                break;
            }
            if (simElapsedMs >= maxSimMs) break;
        }

        // ── 5. Summary ────────────────────────────────────────────────────────
        var totalWallMs = wallClock.Elapsed.TotalMilliseconds;
        var totalSimMs  = pico.Cpu.Cycles / (RP2040_CLK_HZ / 1_000.0);
        var blinkSimS   = (totalSimMs - blinkSimMs0) / 1000.0;

        Console.WriteLine(new string('─', 60));
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine("Blink demo summary");
        Console.ResetColor();
        Console.WriteLine($"  Iterations programmed   : {iterations}");
        Console.WriteLine($"  GPIO 25 transitions seen: {transitions}");
        Console.WriteLine($"  Final LED state         : {(lastLed ? "HIGH" : "LOW")}");
        Console.WriteLine($"  Blink-loop simulated time: {blinkSimS:F2} s");
        Console.WriteLine($"  Total wall-clock time   : {FormatTime(totalWallMs)}");
        Console.WriteLine($"  Total simulated time    : {totalSimMs / 1000.0:F2} s");

        if (blinkSimS < TargetRunSeconds)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine($"  Note: blink loop ran for less than the {TargetRunSeconds:F0} s target.");
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine();
        Console.WriteLine($"  ✓ Demo ran for {blinkSimS:F2} s of simulated time (≥ {TargetRunSeconds:F0} s target).");
        Console.ResetColor();
        return 0;
    }

    /// <summary>
    /// Run the simulation until CircuitPython prints its REPL prompt.  CircuitPython 9.x
    /// emits "Press any key to enter the REPL." when no code.py is present — when we see
    /// that, we send a single CR to advance past it.  Returns true if "&gt;&gt;&gt; " is observed
    /// before <paramref name="timeoutMs"/> elapses.
    /// </summary>
    private static bool WaitForRepl(PicoSimulation pico, double timeoutMs)
    {
        const double batchMs = 100.0;
        var elapsed = 0.0;
        var keySent = false;
        while (elapsed < timeoutMs)
        {
            pico.RunMilliseconds(batchMs);
            elapsed += batchMs;

            if (!keySent && pico.UsbCdc.Text.Contains("Press any key", StringComparison.OrdinalIgnoreCase))
            {
                pico.UsbCdc.InjectString("\r");
                keySent = true;
            }

            if (pico.UsbCdc.Text.Contains(">>> ", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string FormatTime(double ms) =>
        ms < 1000 ? $"{ms:F0} ms" : $"{ms / 1000.0:F2} s";

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   RP2040Sharp Demo — CircuitPython Blink (board.LED)     ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    // ── Firmware download ─────────────────────────────────────────────────────

    private static readonly string CacheDir =
        Path.Combine(Path.GetTempPath(), "rp2040sharp-firmware-cache");

    /// <summary>
    /// Downloads the official CircuitPython UF2 image for the Raspberry Pi Pico and
    /// caches it under the system temp directory so subsequent runs are offline.
    /// </summary>
    private static async Task<string?> DownloadFirmwareAsync(string version)
    {
        Directory.CreateDirectory(CacheDir);

        var tag = version.StartsWith('v') ? version[1..] : version;
        var path = Path.Combine(CacheDir, $"circuitpython-{tag}.uf2");

        if (File.Exists(path) && new FileInfo(path).Length > 0)
            return path;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("RP2040Sharp-Demo/1.0");

            var url = $"https://downloads.circuitpython.org/bin/raspberry_pi_pico/en_US/" +
                      $"adafruit-circuitpython-raspberry_pi_pico-en_US-{tag}.uf2";

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
}
