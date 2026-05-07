using RP2040.Peripherals;
using RP2040.TestKit.Boards;
using RP2040.TestKit.Extensions;
using RP2040Sharp.IntegrationTests.Firmware;

namespace RP2040Sharp.IntegrationTests.Tests;

/// <summary>
/// Integration tests for System examples from pico-examples.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SystemTests
{
    // ── unique_board_id ───────────────────────────────────────────────────────

    [Fact]
    public void UniqueBoardId_NoHardFault_AfterIdRead()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.UniqueBoardId)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(500);

        pico.Cpu.Registers.IPSR.Should().NotBe(3u,
            "HardFault must not occur while reading unique board ID via SSI/DMA");
    }

    [Fact]
    public void UniqueBoardId_Uart0_PrintsBoardId()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.UniqueBoardId)!;

        pico.LoadFlash(flash);

        // unique_board_id reads the 8-byte flash UID via SSI and prints it as hex over UART0
        var found = pico.RunUntilOutput(pico.Uart0, text => text.Length > 0, timeoutMs: 5_000);

        found.Should().BeTrue("unique_board_id must print the board ID over UART0");
    }

    [Fact]
    public void UniqueBoardId_Uart0_OutputLooksLikeHexId()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.UniqueBoardId)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(2_000);

        var text = pico.Uart0.Text;
        text.Should().NotBeEmpty("unique_board_id must have produced output");

        // Board ID is 8 bytes printed as 16 hex characters
        var hasHexChars = text.Any(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
        hasHexChars.Should().BeTrue("the board ID output should contain hexadecimal characters");
    }

    [Fact]
    public void UniqueBoardId_Cpu_CompletesWithoutStackCorruption()
    {
        using var pico = new PicoSimulation();
        var flash = RP2040Machine.Uf2ToFlash(PicoExamplesFirmware.UniqueBoardId)!;

        pico.LoadFlash(flash);
        pico.RunMilliseconds(1_000);

        pico.Cpu.Registers.SP.Should().BeInRange(0x2000_0000u, 0x2004_2000u,
            "SP must remain in SRAM after SSI/DMA flash ID read");
    }
}
