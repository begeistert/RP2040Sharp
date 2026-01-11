# RP2040Sharp

![Build Status](https://github.com/begeistert/RP2040Sharp/actions/workflows/test.yml/badge.svg)
![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET Version](https://img.shields.io/badge/.NET-10.0-purple)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=begeistert_RP2040Sharp&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=begeistert_RP2040Sharp)

**RP2040Sharp** is a high-performance emulator for the Raspberry Pi RP2040 microcontroller, written entirely in **modern C# (.NET 10)**.

This project is a port and re-imagination of the excellent [rp2040js](https://github.com/wokwi/rp2040js) project by Uri Shaked. The goal is to bring embedded emulation to the .NET ecosystem with a strong focus on speed and type safety, leveraging the latest runtime features.

> 🚧 **Project Status:** Work in Progress. The CPU core (Cortex-M0+) is under active development and passing instruction tests.

## 🚀 Technical Features

* **Architecture:** Faithful emulation of the **ARM Cortex-M0+** core.
* **Performance:** Heavy use of `Span<T>`, `Unsafe`, and pointers for direct emulated memory access, minimizing Garbage Collector overhead.
* **Bus Interconnect:** Memory mapping system handling Flash, SRAM, BootROM, and Peripherals.
* **Testing:** Robust unit test suite using **xUnit** and **FluentAssertions** to validate the Thumb instruction set.

## 🛠️ Requirements

* **.NET 10 SDK**.
* Visual Studio 2022 or JetBrains Rider.

## 📦 Solution Structure

* `RP2040.Core`: The heart of the emulator. Contains the instruction decoder, registers, memory bus, and CPU logic.
* `RP2040.Peripherals`: Implementation of hardware peripherals (UART, GPIO, PWM, etc.) *[In Development]*.
* `RP2040.Core.Tests`: Unit tests validating opcode execution and logic.

## 💻 Getting Started

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/begeistert/RP2040Sharp.git
    cd RP2040Sharp
    ```

2.  **Restore dependencies and Build:**
    ```bash
    dotnet restore
    dotnet build
    ```

3.  **Run the Tests:**
    The project includes comprehensive tests to validate arithmetic, logic, and flow control instructions.
    ```bash
    dotnet test
    ```

## 🗺️ Roadmap

### Core Emulation
- [x] Basic Instruction Decoder
- [x] Arithmetic Operations (ADD, SUB, MUL, CMP)
- [x] Bitwise Operations (AND, ORR, EOR, LSL, LSR)
- [x] Flow Control (Branching, BL, BLX)
- [x] Stack Management (PUSH, POP)
- [ ] Exceptions and Interrupts (NVIC)
- [ ] Dual Core Support (SIO)

### Peripherals
- [ ] GPIO & Pin Access
- [ ] UART (Serial Communication)
- [ ] Timer & Alarm System
- [ ] PWM
- [ ] SPI / I2C

### Ecosystem & Targets
- [ ] **Native AOT Compilation:**
    - [ ] Windows (x64/arm64)
    - [ ] Linux (x64/arm64)
    - [ ] macOS (Apple Silicon)
- [ ] **WebAssembly (WASM):** Run RP2040Sharp directly in the browser.
- [ ] Loader for `.elf` and `.uf2` files.
- [ ] GDB Server implementation for debugging.

## 🤝 Contributing

Contributions are welcome! This is a collaborative project.

1.  Fork the repository.
2.  Create a feature branch (`git checkout -b feature/AmazingFeature`).
3.  Ensure **all tests pass**.
4.  Commit your changes (`git commit -m 'Add some AmazingFeature'`).
5.  Push to the branch (`git push origin feature/AmazingFeature`).
6.  Open a Pull Request.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

Based on the original work from [rp2040js](https://github.com/wokwi/rp2040js) © 2021 Uri Shaked.
C# Port © 2025 Iván Montiel Cardona.