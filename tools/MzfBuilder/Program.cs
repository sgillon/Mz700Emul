using System;
using System.IO;
using System.Text;

namespace MzfBuilder;

/// <summary>
/// Tiny console tool that emits hand-assembled MZ-700 .mzf cassette
/// images for use as test programs. Each "build" function returns the
/// raw Z80 byte sequence; <see cref="WriteMzf"/> wraps it in a 128-byte
/// MZF header so the emulator (or a real MZ-700) can load and run it.
///
/// Adding a new test program: write a Build* method, register it in
/// <see cref="Programs"/>, and re-run.
/// </summary>
internal static class Program
{
    private record TestProgram(string Filename, ushort LoadAddr, Func<byte[]> Build);

    private static readonly TestProgram[] Programs =
    {
        new("joytest.mzf",  0x1200, BuildJoyTestRaw),
    };

    private static int Main(string[] args)
    {
        var outDir = args.Length > 0 ? args[0] : ".";
        Directory.CreateDirectory(outDir);
        foreach (var p in Programs)
        {
            var bytes = p.Build();
            var path = Path.Combine(outDir, p.Filename);
            WriteMzf(path, bytes, Path.GetFileNameWithoutExtension(p.Filename).ToUpperInvariant(),
                p.LoadAddr, p.LoadAddr);
            Console.WriteLine($"  {path}  ({bytes.Length} bytes code, {128 + bytes.Length} total)");
        }
        return 0;
    }

    /// <summary>
    /// joytest.mzf: prints "JOY " then the live $E008 byte as 8 binary
    /// digits in row 0 of VRAM, looping forever with a small delay so
    /// updates are visible. Roughly 30 refreshes per second on real
    /// hardware. Tests only the *raw* port read — not the VBLK-synced
    /// JOY() routines from the manual; that's a follow-up program.
    ///
    /// Hand-assembled Z80 (load + exec at $1200):
    ///
    ///   ; --- clear VRAM (1024 bytes at $D000) ---
    ///   LD   HL,$D000
    ///   LD   (HL),0
    ///   LD   DE,$D001
    ///   LD   BC,$03FF
    ///   LDIR
    ///   ; --- print "JOY" at row 0, cols 0-2 ---
    ///   LD   A,$0A           ; 'J'
    ///   LD   ($D000),A
    ///   LD   A,$0F           ; 'O'
    ///   LD   ($D001),A
    ///   LD   A,$19           ; 'Y'
    ///   LD   ($D002),A
    ///   ; --- main: read $E008, render 8 bits at cols 4-11 ---
    ///   main:
    ///   LD   A,($E008)
    ///   LD   B,A
    ///   LD   HL,$D004
    ///   LD   C,8
    ///   bitloop:
    ///   RL   B               ; rotate bit-7 into carry
    ///   LD   A,0
    ///   ADC  A,$20           ; '0' or '1' display code
    ///   LD   (HL),A
    ///   INC  HL
    ///   DEC  C
    ///   JR   NZ,bitloop
    ///   ; --- delay so updates are eye-readable ---
    ///   LD   DE,$4000
    ///   delay:
    ///   DEC  DE
    ///   LD   A,D
    ///   OR   E
    ///   JR   NZ,delay
    ///   JP   main
    /// </summary>
    private static byte[] BuildJoyTestRaw() => new byte[]
    {
        // Clear VRAM ($D000..$D3FF) — 0x00 = blank display code.
        0x21, 0x00, 0xD0,       // LD HL,$D000
        0x36, 0x00,             // LD (HL),0
        0x11, 0x01, 0xD0,       // LD DE,$D001
        0x01, 0xFF, 0x03,       // LD BC,$03FF
        0xED, 0xB0,             // LDIR

        // Header — write "JOY" at $D000-$D002.
        0x3E, 0x0A,             // LD A,$0A   ; 'J'
        0x32, 0x00, 0xD0,       // LD ($D000),A
        0x3E, 0x0F,             // LD A,$0F   ; 'O'
        0x32, 0x01, 0xD0,       // LD ($D001),A
        0x3E, 0x19,             // LD A,$19   ; 'Y'
        0x32, 0x02, 0xD0,       // LD ($D002),A

        // main: ($121C)
        0x3A, 0x08, 0xE0,       // LD A,($E008)
        0x47,                   // LD B,A
        0x21, 0x04, 0xD0,       // LD HL,$D004
        0x0E, 0x08,             // LD C,8

        // bitloop: ($1225)
        0xCB, 0x10,             // RL B
        0x3E, 0x00,             // LD A,0
        0xCE, 0x20,             // ADC A,$20
        0x77,                   // LD (HL),A
        0x23,                   // INC HL
        0x0D,                   // DEC C
        0x20, 0xF5,             // JR NZ,bitloop   ; -11

        // Delay (~30 Hz refresh)
        0x11, 0x00, 0x40,       // LD DE,$4000

        // delay: ($1233)
        0x1B,                   // DEC DE
        0x7A,                   // LD A,D
        0xB3,                   // OR E
        0x20, 0xFB,             // JR NZ,delay     ; -5

        0xC3, 0x1C, 0x12,       // JP main ($121C)
    };

    /// <summary>
    /// Wraps Z80 bytes in a 128-byte MZF header so the emulator's
    /// cassette loader can pick them up.
    /// </summary>
    private static void WriteMzf(string path, byte[] code, string filename, ushort loadAddr, ushort execAddr)
    {
        var mzf = new byte[128 + code.Length];
        mzf[0] = 0x01; // type 0x01 = machine code

        // Filename: ASCII, 0x0D terminator, in bytes 1..17 (max 16 chars + terminator).
        var name = Encoding.ASCII.GetBytes(filename);
        int n = Math.Min(name.Length, 16);
        Array.Copy(name, 0, mzf, 1, n);
        mzf[1 + n] = 0x0D;

        // Size, load, exec — little-endian at offsets 18, 20, 22.
        mzf[18] = (byte)(code.Length & 0xFF);
        mzf[19] = (byte)(code.Length >> 8);
        mzf[20] = (byte)(loadAddr & 0xFF);
        mzf[21] = (byte)(loadAddr >> 8);
        mzf[22] = (byte)(execAddr & 0xFF);
        mzf[23] = (byte)(execAddr >> 8);

        // 24..127 left as zero (comment/padding).
        Array.Copy(code, 0, mzf, 128, code.Length);

        File.WriteAllBytes(path, mzf);
    }
}
