# Z80Core

A cycle-accurate Zilog Z80 CPU emulator in C# / .NET 8.

- Passes both **ZEXDOC** (documented behaviour) and **ZEXALL** (full
  including undocumented flag behaviour) — see `samples/ZexHarness/`.
- Clean-room. No host coupling. The core knows nothing about your
  hardware, your bus, your interrupt source, or your address-space
  overlay.
- Single small library — four C# files plus a single-instruction
  disassembler. No dependencies beyond `System`.
- Designed to be vendored or submoduled into other projects, not
  consumed via NuGet (yet). MIT licensed.

Originally extracted from
[MZRaku](https://github.com/sgillon/MZRaku), a Sharp MZ-700 emulator,
where it powered S-BASIC, all bundled-monitor ROM routines, and a fair
set of commercial cassettes (Star Trek, Space Panic, Nightmare Park,
etc.) without flag-handling or cycle-timing surprises.

## Public surface

```csharp
namespace Z80Core;

public interface IMemory { byte Read(ushort addr); void Write(ushort addr, byte value); }
public interface IIoBus  { byte In(ushort port);   void Out(ushort port, byte value); }

public sealed partial class Z80Cpu
{
    public IMemory Mem;
    public IIoBus  Io;

    public void Reset();
    public int  Step();                      // returns T-state cost
    public void RequestInterrupt();
    public void ClearInterrupt();

    // Registers, flag constants, Halted, TotalCycles, breakpoints,
    // PC trace ring buffer, PreStep hook — see docs/usage.md.
}

public static class Z80Disassembler
{
    public static Result Disassemble(IMemory mem, ushort addr,
                                     Func<ushort, bool>? isSideEffectAddr = null);
}
```

## Minimum host

```csharp
using Z80Core;

class FlatRam : IMemory
{
    private readonly byte[] _b = new byte[0x10000];
    public byte Read(ushort a) => _b[a];
    public void Write(ushort a, byte v) => _b[a] = v;
    public void Load(ushort at, byte[] code) => code.CopyTo(_b, at);
}

class NoIo : IIoBus
{
    public byte In(ushort port) => 0xFF;
    public void Out(ushort port, byte value) { }
}

var ram = new FlatRam();
ram.Load(0x0000, new byte[]
{
    0x3E, 0x42,         // LD A,$42
    0x06, 0x01,         // LD B,$01
    0x80,               // ADD A,B
    0x76,               // HALT
});

var cpu = new Z80Cpu { Mem = ram, Io = new NoIo() };
cpu.Reset();
while (!cpu.Halted) cpu.Step();

Console.WriteLine($"A=${cpu.A:X2} cycles={cpu.TotalCycles}");
// A=$43 cycles=...
```

That's all the wiring there is. For a real host you'll route address
ranges to ROM / RAM / MMIO inside `IMemory.Read/Write` (the core stays
oblivious), and call `RequestInterrupt()` from your peripheral timer
when an IRQ fires. See `docs/usage.md` for the cycle-budgeted run-frame
pattern, and `docs/architecture.md` for the internals.

## Documentation

- [`docs/usage.md`](docs/usage.md) — How to wire the core into a host. Run-frame budgeting. Interrupts.
- [`docs/architecture.md`](docs/architecture.md) — Internal design: partial-class split, opcode decoding, prefix state machine, WZ/MEMPTR, cycle accounting.
- [`docs/debugger-hooks.md`](docs/debugger-hooks.md) — Breakpoints, PC trace, the `PreStep` hook (ROM-trap pattern).
- [`docs/disassembler.md`](docs/disassembler.md) — Single-instruction disassembler, side-effect-aware reads.
- [`docs/zex-validation.md`](docs/zex-validation.md) — Running the bundled ZEX harness.

## Layout

```
Z80Core/                — library source (4 files)
  Z80.cs                — registers, flags, helpers, Step, interrupts
  Z80Main.cs            — main (unprefixed + DD/FD-prefixed) opcode table
  Z80CB.cs              — CB-prefixed rotates / bit ops
  Z80ED.cs              — ED-prefixed block ops, IM, IN/OUT
  Z80Disassembler.cs    — static disassembler
Z80Core.csproj          — net8.0 class library, embedded debug in Release
docs/                   — see above
samples/ZexHarness/     — console app that runs ZEXDOC.com / ZEXALL.com against the core
```

## Building

```
dotnet build
dotnet run --project samples/ZexHarness -- zexdoc.com
```

## Licence

MIT — see [LICENSE](LICENSE). The bundled `zexdoc.com` and `zexall.com`
in `samples/ZexHarness/` are GPL v2; their own licence text lives at
`samples/ZexHarness/Copying`. They are guest programs the harness *runs*
at runtime — see the licence notes in [LICENSE](LICENSE) and
[`docs/zex-validation.md`](docs/zex-validation.md).
