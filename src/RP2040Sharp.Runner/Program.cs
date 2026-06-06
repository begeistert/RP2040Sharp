using System.Text;
using RP2040.Peripherals;
using RP2040.Peripherals.Usb;

namespace RP2040Sharp.Runner;

/// <summary>
/// Headless RP2040 firmware runner for CI pipelines (e.g. validating PyMCU compiler output).
/// Loads a UF2 or raw flash image, runs it under a bounded instruction budget — so it can
/// never hang the build — and checks the serial output for an expected string.
///
///   rp2040sharp &lt;image.uf2&gt; --expect-text "PASS" [--channel uart|usb] [--max-instructions N]
///
/// Serial output goes to stdout; the run summary goes to stderr. Exit codes:
///   0  expected text found (or no --expect-text given and the run did not crash)
///   1  expected text not found within the instruction budget
///   2  the CPU locked up (HardFault escalation — the firmware crashed)
///   64 usage error
///   66 image file not found
/// </summary>
internal static class Program
{
    private const int ExitOk        = 0;
    private const int ExitFailed    = 1;
    private const int ExitCrashed   = 2;
    private const int ExitUsage     = 64;
    private const int ExitNoInput   = 66;

    private static int Main(string[] args)
    {
        string? imagePath = null;
        string? expectText = null;
        var channel = "uart";
        long maxInstructions = 500_000_000;
        var quiet = false;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "-h" or "--help":
                    PrintUsage(Console.Out);
                    return ExitOk;
                case "--expect-text":
                    if (++i >= args.Length) return Usage("--expect-text requires a value");
                    expectText = args[i];
                    break;
                case "--channel":
                    if (++i >= args.Length) return Usage("--channel requires a value");
                    channel = args[i].ToLowerInvariant();
                    if (channel is not ("uart" or "usb")) return Usage($"unknown channel '{channel}' (use uart|usb)");
                    break;
                case "--max-instructions":
                    if (++i >= args.Length || !long.TryParse(args[i], out maxInstructions) || maxInstructions <= 0)
                        return Usage("--max-instructions requires a positive integer");
                    break;
                case "--image":
                    if (++i >= args.Length) return Usage("--image requires a path");
                    imagePath = args[i];
                    break;
                case "--quiet":
                    quiet = true;
                    break;
                default:
                    if (a.StartsWith('-')) return Usage($"unknown option '{a}'");
                    if (imagePath != null) return Usage("more than one image given");
                    imagePath = a;
                    break;
            }
        }

        if (imagePath is null) return Usage("no firmware image given");
        if (!File.Exists(imagePath))
        {
            Console.Error.WriteLine($"error: image not found: {imagePath}");
            return ExitNoInput;
        }

        var bytes = File.ReadAllBytes(imagePath);
        var machine = new RP2040Machine();
        var flash = RP2040Machine.Uf2ToFlash(bytes);   // null when the bytes aren't a UF2
        machine.LoadFlash(flash ?? bytes);

        // ── Capture serial output on the requested channel ───────────────────────
        var output = new StringBuilder();
        void Emit(byte b)
        {
            output.Append((char)b);
            if (!quiet) { Console.Out.Write((char)b); Console.Out.Flush(); }
        }

        if (channel == "uart")
        {
            machine.Uart0.OnByteTransmit += Emit;
        }
        else
        {
            var cdc = new UsbCdcHost(machine.Usb);
            cdc.OnSerialData += data => { foreach (var b in data) Emit(b); };
        }

        // ── Bounded run: never hangs ─────────────────────────────────────────────
        const int batch = 100_000;
        var start = machine.InstructionCount;
        var crashed = false;
        var found = false;

        while (machine.InstructionCount - start < maxInstructions)
        {
            if (expectText != null && output.ToString().Contains(expectText, StringComparison.Ordinal))
            {
                found = true;
                break;
            }
            if (machine.Cpu.IsLockedUp)
            {
                crashed = true;
                break;
            }
            machine.Run(batch);
        }

        // Final re-check after the last batch.
        if (!found && expectText != null && output.ToString().Contains(expectText, StringComparison.Ordinal))
            found = true;
        if (!crashed && machine.Cpu.IsLockedUp)
            crashed = true;

        var executed = machine.InstructionCount - start;

        // Finding the expected text wins: the firmware did its job. A program that prints a
        // result and then spins / bkpts / faults still passes — that's normal for test stubs.
        if (expectText != null && found)
        {
            Console.Error.WriteLine($"OK: found \"{expectText}\" after {executed} instructions.");
            return ExitOk;
        }

        if (crashed)
        {
            Console.Error.WriteLine($"FAIL: CPU locked up after {executed} instructions " +
                                    $"(PC=0x{machine.Cpu.Registers.PC:X8}, IPSR={machine.Cpu.Registers.IPSR}).");
            return ExitCrashed;
        }

        if (expectText != null)
        {
            Console.Error.WriteLine($"FAIL: \"{expectText}\" not seen within {maxInstructions} instructions " +
                                    $"(executed {executed}" +
                                    (machine.Cpu.Registers.Waiting ? ", CPU was in WFI/WFE" : "") + ").");
            return ExitFailed;
        }

        Console.Error.WriteLine($"OK: ran {executed} instructions, no crash.");
        return ExitOk;
    }

    private static int Usage(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        PrintUsage(Console.Error);
        return ExitUsage;
    }

    private static void PrintUsage(TextWriter w)
    {
        w.WriteLine("Usage: rp2040sharp <image.uf2|image.bin> [options]");
        w.WriteLine();
        w.WriteLine("Options:");
        w.WriteLine("  --expect-text <text>     Pass (exit 0) only if <text> appears in serial output");
        w.WriteLine("  --channel uart|usb       Serial channel to watch (default: uart)");
        w.WriteLine("  --max-instructions <n>   Hard execution budget (default: 500000000)");
        w.WriteLine("  --quiet                  Do not echo serial output to stdout");
        w.WriteLine("  -h, --help               Show this help");
        w.WriteLine();
        w.WriteLine("Exit codes: 0 ok · 1 text not found · 2 firmware crashed · 64 usage · 66 no input");
    }
}
