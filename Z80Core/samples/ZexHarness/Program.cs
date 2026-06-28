// ZexHarness: run a CP/M-style .com Z80 test program (ZEXDOC, ZEXALL,
// or any other that uses only BDOS functions 2 and 9) against
// Z80Core's Z80Cpu. Output streams to stdout as the test runs.
//
// Usage:
//   dotnet run --project samples/ZexHarness -- zexdoc.com
//   dotnet run --project samples/ZexHarness -- path/to/program.com
//
// If no path is given, defaults to zexdoc.com next to the exe.

using Z80Core;

if (args.Length > 1)
{
    Console.Error.WriteLine("Usage: ZexHarness [path-to-comfile]");
    return 2;
}

string comPath = args.Length == 1
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "zexdoc.com");

if (!File.Exists(comPath))
{
    Console.Error.WriteLine($"ZexHarness: {comPath}: file not found");
    return 1;
}

byte[] comBytes = File.ReadAllBytes(comPath);

var mem = new FlatMemory();
var io = new NoIo();
var cpu = new Z80Cpu { Mem = mem, Io = io };

// Load .com at the CP/M TPA.
Array.Copy(comBytes, 0, mem.Bytes, 0x0100, comBytes.Length);

// CP/M trampoline:
//   $0000: HLT — WBOOT vector; PreStep recognises PC=$0000 as exit.
//   $0005: JP $E000 — BDOS vector. PreStep intercepts PC=$0005 before
//          execution and emulates the call directly. The JP target
//          ($E000) is only material because ZEX reads the word at
//          $0006 with `LD HL,(6) / LD SP,HL` to find the top of usable
//          memory.
mem.Bytes[0x0000] = 0x76;                 // HLT
mem.Bytes[0x0005] = 0xC3;                 // JP nn
mem.Bytes[0x0006] = 0x00;
mem.Bytes[0x0007] = 0xE0;

cpu.Reset();
cpu.PC = 0x0100;
cpu.SP = 0xE000;
cpu.IFF1 = cpu.IFF2 = false;
cpu.IM = 1;
cpu.Halted = false;

bool shouldExit = false;
var lineBuf = new System.Text.StringBuilder();

void Flush()
{
    if (lineBuf.Length == 0) return;
    Console.Write(lineBuf.ToString());
    lineBuf.Clear();
}

void EmitChar(char ch)
{
    lineBuf.Append(ch);
    if (ch == '\n') Flush();
}

cpu.PreStep = () =>
{
    if (cpu.PC == 0x0005)
    {
        // BDOS call. Function in C.
        switch (cpu.C)
        {
            case 2:
                // Console output: character in E.
                EmitChar((char)cpu.E);
                break;
            case 9:
                // Print '$'-terminated string at DE.
                ushort addr = cpu.DE;
                for (int safety = 0; safety < 65536; safety++)
                {
                    byte b = mem.Read(addr++);
                    if (b == (byte)'$') break;
                    EmitChar((char)b);
                }
                break;
            // Other BDOS functions are silently ignored — ZEX doesn't use them.
        }

        // Synthesise RET: pop return address into PC.
        byte lo = mem.Read(cpu.SP);
        byte hi = mem.Read((ushort)(cpu.SP + 1));
        cpu.SP += 2;
        cpu.PC = (ushort)((hi << 8) | lo);
        return true;
    }

    if (cpu.PC == 0x0000)
    {
        // CP/M WBOOT vector — program is done.
        shouldExit = true;
        return true;
    }

    return false;
};

Console.CancelKeyPress += (_, e) => { e.Cancel = true; shouldExit = true; };

try
{
    while (!shouldExit) cpu.Step();
}
finally
{
    Flush();
}

return 0;

sealed class FlatMemory : IMemory
{
    public readonly byte[] Bytes = new byte[0x10000];
    public byte Read(ushort addr) => Bytes[addr];
    public void Write(ushort addr, byte value) => Bytes[addr] = value;
}

sealed class NoIo : IIoBus
{
    public byte In(ushort port) => 0xFF;
    public void Out(ushort port, byte value) { }
}
