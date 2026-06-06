# Validating firmware in CI

RP2040Sharp is designed to validate compiler/firmware output (for example, the
[PyMCU](https://docs.pymcu.org) compiler) in CI **without flaky or hanging builds**:

- Runs are **bounded** — a wedged program fails with a reason instead of stalling the job.
- The clock is driven by executed cycles, so results are **deterministic** and reproducible.

There are two ways to use it in a pipeline.

## In a .NET test project (recommended)

Use the TestKit directly from xUnit/NUnit. This is what PyMCU's integration suite does:

```csharp
[Fact]
public void Blink_firmware_reports_pass()
{
    using var pico = new PicoSimulation();
    pico.LoadFlash(RP2040Machine.Uf2ToFlash(File.ReadAllBytes("blink.uf2"))!);

    var result = pico.RunUntilHalt(pico.Uart0, "PASS");

    result.Succeeded.Should().BeTrue($"firmware halted with {result.Outcome}");
    pico.Cpu.Should().NotHaveFaulted();
}
```

## Headless runner CLI

For pipelines that just need an exit code (no C# harness), use the `rp2040sharp` runner:

```bash
dotnet run --project src/RP2040Sharp.Runner -c Release -- \
    firmware.uf2 --expect-text "PASS" --channel uart --max-instructions 5000000
```

Exit codes:

| Code | Meaning |
|---|---|
| `0` | expected text found |
| `1` | text not found within the instruction budget |
| `2` | the firmware crashed (CPU lockup) |
| `64` | usage error |
| `66` | image file not found |

Options:

| Option | Default | Description |
|---|---|---|
| `--expect-text <text>` | — | Pass only if `<text>` appears in serial output |
| `--channel uart\|usb` | `uart` | Serial channel to watch |
| `--max-instructions <n>` | `500000000` | Hard execution budget |
| `--quiet` | off | Do not echo serial output to stdout |

Serial output goes to **stdout**; the run summary goes to **stderr**.
