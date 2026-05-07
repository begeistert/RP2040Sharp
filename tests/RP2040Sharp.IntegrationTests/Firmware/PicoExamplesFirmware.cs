namespace RP2040Sharp.IntegrationTests.Firmware;

/// <summary>
/// Pre-compiled RP2040 firmware images (raw UF2 bytes) embedded as assembly resources.
/// These binaries were compiled with pico-sdk 2.1.0 for the Pico board.
///
/// Use <see cref="RP2040Sharp.Peripherals.RP2040Machine.Uf2ToFlash"/> to convert to
/// raw flash bytes before loading into a simulation.
/// </summary>
internal static class PicoExamplesFirmware
{
    // --- GPIO / Blink ---

    /// <summary>blink/blink.uf2 — GPIO 25 blinks at 1 Hz (500 ms ON / 500 ms OFF).</summary>
    public static byte[] Blink => Load("Firmware.gpio.blink.uf2");

    /// <summary>blink_simple/blink_simple.uf2 — Minimal GPIO 25 blink.</summary>
    public static byte[] BlinkSimple => Load("Firmware.gpio.blink_simple.uf2");

    /// <summary>gpio/hello_gpio_irq — GPIO interrupt-driven button example.</summary>
    public static byte[] HelloGpioIrq => Load("Firmware.gpio.hello_gpio_irq.uf2");

    // --- UART ---

    /// <summary>hello_world/serial — "Hello, World!" over UART0.</summary>
    public static byte[] HelloSerial => Load("Firmware.uart.hello_serial.uf2");

    /// <summary>uart/hello_uart — UART peripheral demo with configurable baud rate.</summary>
    public static byte[] HelloUart => Load("Firmware.uart.hello_uart.uf2");

    // --- USB ---

    /// <summary>hello_world/usb — "Hello, World!" over USB CDC.</summary>
    public static byte[] HelloUsb => Load("Firmware.usb.hello_usb.uf2");

    // --- Timer ---

    /// <summary>timer/hello_timer — Repeating timer callbacks via SDK timer API.</summary>
    public static byte[] HelloTimer => Load("Firmware.timer.hello_timer.uf2");

    /// <summary>timer/timer_lowlevel — Direct hardware timer register access.</summary>
    public static byte[] TimerLowlevel => Load("Firmware.timer.timer_lowlevel.uf2");

    // --- PWM ---

    /// <summary>pwm/hello_pwm — Basic PWM output on GPIO.</summary>
    public static byte[] HelloPwm => Load("Firmware.pwm.hello_pwm.uf2");

    /// <summary>pwm/led_fade — PWM LED brightness fade using slice wrap/level.</summary>
    public static byte[] PwmLedFade => Load("Firmware.pwm.pwm_led_fade.uf2");

    // --- DMA ---

    /// <summary>dma/hello_dma — Basic DMA memory-to-memory copy.</summary>
    public static byte[] HelloDma => Load("Firmware.dma.hello_dma.uf2");

    /// <summary>dma/channel_irq — DMA transfer completion interrupt.</summary>
    public static byte[] DmaChannelIrq => Load("Firmware.dma.dma_channel_irq.uf2");

    // --- Watchdog ---

    /// <summary>watchdog/hello_watchdog — Watchdog timer scratch/reboot demo.</summary>
    public static byte[] HelloWatchdog => Load("Firmware.watchdog.hello_watchdog.uf2");

    // --- RTC ---

    /// <summary>rtc/hello_rtc — RTC time/date set and read via UART.</summary>
    public static byte[] HelloRtc => Load("Firmware.rtc.hello_rtc.uf2");

    /// <summary>rtc/rtc_alarm — RTC one-shot alarm callback.</summary>
    public static byte[] RtcAlarm => Load("Firmware.rtc.rtc_alarm.uf2");

    // --- Multicore ---

    /// <summary>multicore/hello_multicore — Launch code on core 1 via SIO FIFO.</summary>
    public static byte[] HelloMulticore => Load("Firmware.multicore.hello_multicore.uf2");

    /// <summary>multicore/multicore_fifo_irqs — Inter-core FIFO IRQ communication.</summary>
    public static byte[] MulticoreFifoIrqs => Load("Firmware.multicore.multicore_fifo_irqs.uf2");

    // --- PIO ---

    /// <summary>pio/hello_pio — Minimal PIO blink program, state machine setup.</summary>
    public static byte[] HelloPio => Load("Firmware.pio.hello_pio.uf2");

    /// <summary>pio/pio_blink — PIO-driven GPIO blink with configurable period.</summary>
    public static byte[] PioBlink => Load("Firmware.pio.pio_blink.uf2");

    /// <summary>pio/uart_tx — PIO UART transmitter (8N1).</summary>
    public static byte[] PioUartTx => Load("Firmware.pio.pio_uart_tx.uf2");

    // --- Interpolator ---

    /// <summary>interp/hello_interp — SIO interpolator lanes, accumulators and base offsets.</summary>
    public static byte[] HelloInterp => Load("Firmware.interp.hello_interp.uf2");

    // --- Hardware Divider ---

    /// <summary>divider/hello_divider — SIO hardware divider (signed/unsigned).</summary>
    public static byte[] HelloDivider => Load("Firmware.divider.hello_divider.uf2");

    // --- ADC ---

    /// <summary>adc/hello_adc — ADC channel read and print via UART.</summary>
    public static byte[] HelloAdc => Load("Firmware.adc.hello_adc.uf2");

    /// <summary>adc/onboard_temperature — Internal temperature sensor via ADC4.</summary>
    public static byte[] OnboardTemperature => Load("Firmware.adc.onboard_temperature.uf2");

    // --- Clocks ---

    /// <summary>clocks/hello_48MHz — Reconfigure system clock to 48 MHz.</summary>
    public static byte[] Hello48MHz => Load("Firmware.clocks.hello_48MHz.uf2");

    // --- Reset ---

    /// <summary>reset/hello_reset — Peripheral reset subsystem demo.</summary>
    public static byte[] HelloReset => Load("Firmware.reset.hello_reset.uf2");

    // --- System ---

    /// <summary>system/unique_board_id — Read unique board ID from flash via SSI/DMA.</summary>
    public static byte[] UniqueBoardId => Load("Firmware.system.unique_board_id.uf2");

    // ── private loader ────────────────────────────────────────────────────────

    private static byte[] Load(string resourceSuffix)
    {
        var asm = typeof(PicoExamplesFirmware).Assembly;
        var name = $"RP2040Sharp.IntegrationTests.{resourceSuffix}";
        using var stream = asm.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException(
                $"Embedded firmware not found: '{name}'. " +
                "Ensure the .uf2 file is present in the Firmware/ directory and " +
                "the project has <EmbeddedResource Include=\"Firmware\\**\\*.uf2\" />.");
        var buf = new byte[stream.Length];
        _ = stream.Read(buf, 0, buf.Length);
        return buf;
    }
}
