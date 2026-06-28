# ZexHarness

A console-app harness that runs CP/M-style `.com` Z80 test programs
against Z80Core. Bundled with **ZEXDOC** (documented behaviour) and
**ZEXALL** (documented + undocumented).

## Running

From the Z80Core repo root:

```
dotnet run --project samples/ZexHarness -- zexdoc.com
dotnet run --project samples/ZexHarness -- zexall.com
```

Output streams to stdout as each test group finishes. Every line
should end in `OK`:

```
Z80 instruction exerciser
<adc,sbc> hl,<bc,de,hl,sp>....  OK
add hl,<bc,de,hl,sp>..........  OK
...
Tests complete
```

A line ending in `ERROR <expected> <got>` means the core produced the
wrong CRC for that opcode group — fix the underlying executor before
shipping.

Times on a modern desktop machine:
- ZEXDOC: ~6–10 minutes
- ZEXALL: 30–60 minutes (more cases, including undocumented flag bits)

Ctrl-C cleanly aborts.

## Custom programs

The harness accepts any CP/M `.com` file as its first argument, as
long as the program uses only BDOS functions 2 (single-char output)
and 9 (`$`-terminated string output). The two ZEX programs were
written specifically against that subset.

```
dotnet run --project samples/ZexHarness -- path/to/myprogram.com
```

## How it works

See [`../../docs/zex-validation.md`](../../docs/zex-validation.md) for
the architectural notes — the harness is a minimal CP/M-style host
built entirely on Z80Core's public surface (`IMemory`, `IIoBus`,
`Z80Cpu`, `PreStep`). It doubles as a worked example of those APIs.

## Licence notes

- `Program.cs`, `ZexHarness.csproj`, and this `README.md` are **MIT**,
  the same as Z80Core itself.
- `zexdoc.com`, `zexall.com`, `zexdoc.src`, and `zexall.src` are
  Frank Cringle's original distribution, unmodified, licensed under
  the **GNU GPL v2**. See [`Copying`](Copying) for the licence text.

The GPL'd files are guest programs the harness *runs* (they execute
inside the emulated Z80 address space at runtime); they aren't linked
into the harness binary. This is "mere aggregation" under GPL v2 §2
and has no impact on the MIT licence of the harness host code or the
Z80Core library.
