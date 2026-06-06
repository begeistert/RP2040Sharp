# Changelog

All notable changes to **RP2040Sharp** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **CI guardrails for the TestKit**, aimed at using the emulator to validate compiler
  output (e.g. PyMCU) without flaky or hanging builds:
  - `RP2040TestSimulation.RunUntilHalt(predicate, maxInstructions)` — a bounded run that
    can never hang: it returns a diagnostic `RunResult` (`PredicateMet` / `LockedUp` /
    `BudgetReached`) instead of stalling on wedged firmware.
  - CPU-health assertions: `Should().NotBeLockedUp()`, `NotHaveFaulted()`,
    `BeInThreadMode()`, and `HaveExecutedAtMost(n)`.
  - `RP2040TestSimulation.InstructionCount` — a deterministic, reproducible
    instruction-count metric for compiler-size regression checks.
- **`rp2040sharp` runner CLI** (`src/RP2040Sharp.Runner`) — loads a UF2/bin, runs it under
  a hard instruction budget, watches UART or USB-CDC for an expected string, and exits with
  a CI-friendly code (0 found · 1 not found · 2 firmware crashed). The `rp2040js`-style
  `--expect-text` workflow, headless.

## [1.0.0-rc.2] - 2026-06-06

### Changed

- **License hygiene:** pinned `FluentAssertions` to **6.12.0**, the last
  MIT-licensed release (7.x+ moved to a commercial Xceed license). This keeps the
  published `RP2040Sharp.TestKit` package and everything it pulls in MIT-compatible.
  The TestKit's custom assertions were ported back to the 6.x extension model.

## [1.0.0-rc.1] - 2026-06-06

First public release candidate. A high-performance RP2040 emulator in modern C#
(.NET 10) that runs real, unmodified firmware including MicroPython. C# port of
[rp2040js](https://github.com/wokwi/rp2040js) by Uri Shaked.

### Added

- **ARM Cortex-M0+ core** — full Thumb-1 instruction set, exceptions, NVIC,
  SysTick and PendSV, with an O(1) flat-table instruction decoder.
- **Dual-core support** — Core 1 launches through the SIO FIFO multicore
  handshake (RP2040 §2.8.3); both cores advance in lock-step.
- **GDB Remote Serial Protocol server** — debug Core 0 with `arm-none-eabi-gdb`
  via `target remote :3333` (registers, memory, single-step, breakpoints,
  detach). The demo accepts a `--gdb` flag.
- **Real RP2040 B1 BootROM** embedded as a resource; ROM table lookups and
  bit-manipulation helpers run natively.
- **Flash erase/program** through native C# hooks, so MicroPython's LittleFS
  filesystem works correctly.
- **MicroPython** boots to an interactive REPL over emulated USB-CDC.
- **Peripherals** — GPIO, SIO (spinlocks, interpolators, hardware divider),
  UART0/1, SPI0/1, I2C0/1 (master and slave-mode simulation), ADC, PWM (all 8
  slices), PIO0/1 (state machines + GPIO), DMA (all channels, DREQ sources),
  Timer/Alarms, Watchdog, RTC, USB CDC-ACM host, Clocks and Resets.
- **Per-pin GPIO API** (`SetGpioExternalIn`, `GetGpioOut`,
  `GetGpioOutputEnable`) for embedding in circuit simulators.
- **RP2040Sharp.TestKit** — a fluent harness for writing firmware integration
  tests.
- NuGet packaging for `RP2040Sharp` and `RP2040Sharp.TestKit` (AOT-compatible,
  trimmable), versioned automatically from git tags via MinVer.

### Known limitations

- XOSC, ROSC, PLL, PSM and VREG are register stubs (report stable/locked; no
  frequency model).
- Flash programming uses C# hooks rather than the SSI XIP hardware path.
- USB host support is CDC-only (sufficient for the MicroPython REPL).

[Unreleased]: https://github.com/PyMCU/RP2040Sharp/compare/v1.0.0-rc.2...HEAD
[1.0.0-rc.2]: https://github.com/PyMCU/RP2040Sharp/compare/v1.0.0-rc.1...v1.0.0-rc.2
[1.0.0-rc.1]: https://github.com/PyMCU/RP2040Sharp/releases/tag/v1.0.0-rc.1
