using RP2040.Gdb;
using RP2040.Peripherals;

namespace RP2040.Gdb.Tests;

/// <summary>
/// In-process tests for the GDB Remote Serial Protocol server (no sockets).
/// </summary>
public class GdbServerTests
{
    private sealed class TestTarget(RP2040Machine machine) : IGdbTarget
    {
        public RP2040Machine Machine => machine;
        public bool Executing { get; private set; }
        public void Execute() => Executing = true;
        public void Stop() => Executing = false;
    }

    private static (GdbServer server, RP2040Machine machine, TestTarget target) NewServer()
    {
        var machine = new RP2040Machine();
        var target = new TestTarget(machine);
        return (new GdbServer(target), machine, target);
    }

    /// <summary>Strip the GDB framing ($..#cc) to compare the payload.</summary>
    private static string Payload(string? message)
    {
        message.Should().NotBeNull();
        message!.Should().StartWith("$").And.Contain("#");
        return message[1..message.IndexOf('#')];
    }

    [Fact]
    public void Halt_reason_query_reports_trap()
    {
        var (server, _, _) = NewServer();
        Payload(server.ProcessGdbMessage("?")).Should().Be(GdbServer.StopReplyTrap);
    }

    [Fact]
    public void qSupported_advertises_features()
    {
        var (server, _, _) = NewServer();
        Payload(server.ProcessGdbMessage("qSupported:multiprocess+"))
            .Should().Contain("PacketSize").And.Contain("qXfer:features:read+");
    }

    [Fact]
    public void Target_xml_is_served()
    {
        var (server, _, _) = NewServer();
        var payload = Payload(server.ProcessGdbMessage("qXfer:features:read:target.xml:0,fff"));
        payload.Should().StartWith("l");
        payload.Should().Contain("org.gnu.gdb.arm.m-profile");
    }

    [Fact]
    public void Read_all_registers_returns_17_words_little_endian()
    {
        var (server, machine, _) = NewServer();
        machine.Cpu.Registers[0] = 0x11223344;
        machine.Cpu.Registers[15] = 0xCAFEBABE;   // PC

        var payload = Payload(server.ProcessGdbMessage("g"));

        payload.Length.Should().Be(17 * 8, "16 GPRs + xPSR, 4 bytes each as hex");
        payload.Should().StartWith("44332211", "r0 is encoded little-endian");
        payload.Substring(15 * 8, 8).Should().Be("bebafeca", "r15/pc little-endian");
    }

    [Fact]
    public void Read_single_register()
    {
        var (server, machine, _) = NewServer();
        machine.Cpu.Registers[3] = 0xDEADBEEF;
        Payload(server.ProcessGdbMessage("p3")).Should().Be("efbeadde");
    }

    [Fact]
    public void Write_single_register()
    {
        var (server, machine, _) = NewServer();
        // P5=<value little-endian hex>
        Payload(server.ProcessGdbMessage("P5=78563412")).Should().Be("OK");
        machine.Cpu.Registers[5].Should().Be(0x12345678u);
    }

    [Fact]
    public void Read_memory()
    {
        var (server, machine, _) = NewServer();
        machine.Bus.WriteWord(0x2000_0000, 0x04030201);

        Payload(server.ProcessGdbMessage("m20000000,4")).Should().Be("01020304");
    }

    [Fact]
    public void Write_memory()
    {
        var (server, machine, _) = NewServer();
        Payload(server.ProcessGdbMessage("M20000000,4:0a0b0c0d")).Should().Be("OK");

        machine.Bus.ReadByte(0x2000_0000).Should().Be(0x0A);
        machine.Bus.ReadByte(0x2000_0003).Should().Be(0x0D);
    }

    [Fact]
    public void Single_step_executes_one_instruction()
    {
        var (server, machine, _) = NewServer();
        machine.Bus.WriteHalfWord(0x2000_0000, 0xBF00);   // NOP
        machine.Cpu.Registers.PC = 0x2000_0000;

        var payload = Payload(server.ProcessGdbMessage("vCont;s"));

        payload.Should().StartWith("T05").And.Contain("reason:trace");
        machine.Cpu.Registers.PC.Should().Be(0x2000_0002u, "PC advanced past the NOP");
    }

    [Fact]
    public void Continue_starts_execution()
    {
        var (server, _, target) = NewServer();
        server.ProcessGdbMessage("c");
        target.Executing.Should().BeTrue();
    }

    [Fact]
    public void Detach_acknowledges_and_resumes_execution()
    {
        var (server, _, target) = NewServer();
        Payload(server.ProcessGdbMessage("D")).Should().Be("OK");
        target.Executing.Should().BeTrue("detach leaves the target free-running");
    }

    [Fact]
    public void Breakpoint_hit_stops_target_and_reports_trap()
    {
        var (server, machine, target) = NewServer();

        var responses = new List<string>();
        _ = new GdbConnection(server, responses.Add);   // registers the breakpoint handler
        target.Execute();

        machine.Bus.WriteHalfWord(0x2000_0000, 0xBE00);  // BKPT #0
        machine.Cpu.Registers.PC = 0x2000_0000;
        machine.Cpu.Step();

        target.Executing.Should().BeFalse("a breakpoint pauses the target");
        machine.Cpu.Registers.PC.Should().Be(0x2000_0000u, "PC is rewound to the BKPT address");
        responses.Should().Contain(GdbUtils.GdbMessage(GdbServer.StopReplyTrap));
    }

    [Fact]
    public void Connection_parses_a_framed_packet_and_acks()
    {
        var (server, machine, _) = NewServer();
        machine.Cpu.Registers[1] = 0xAABBCCDD;

        var responses = new List<string>();
        var conn = new GdbConnection(server, responses.Add);  // initial '+' ack on construct
        conn.FeedData(GdbUtils.GdbMessage("p1"));

        responses.Should().Contain("+");
        responses.Last().Should().Be(GdbUtils.GdbMessage("ddccbbaa"), "r1 little-endian");
    }
}
