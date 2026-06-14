using System.Drawing;
using System.Windows.Forms;
using MZ700Emul.Hardware;

namespace MZ700Emul;

/// <summary>
/// Diagnostic view of the loaded MZ-700 character ROM — all 512 glyphs
/// (two banks × 256 codes) rendered via <see cref="VideoRenderer.GetGlyph"/>
/// in display-code order. Position implies code: row*16 + col, with bank 0
/// in the top half and bank 1 in the bottom half.
///
/// Click any cell to "type" its display code into the emulator: the form
/// looks up the (row, col, shift) that produces the code via
/// <see cref="RomKeyTables"/> and enqueues it through the existing
/// auto-typer. Useful for reaching MZ-only glyphs (graphics blocks, kana)
/// that don't have a PC-keyboard equivalent.
/// </summary>
public sealed class FontSheetForm : Form
{
    private const int CellsPerRow = 16;
    private const int RowsPerBank = 16;
    private const int GlyphScale = 3;
    private const int CellPx = 8 * GlyphScale;
    private const int MarginPx = 24;
    private const int BankGapPx = 16;

    private static readonly int Bank0Y = MarginPx;
    private static readonly int Bank1Y = MarginPx + RowsPerBank * CellPx + BankGapPx + MarginPx;

    private readonly MZ700 _machine;
    private readonly PictureBox _pic = new() { SizeMode = PictureBoxSizeMode.AutoSize };
    private readonly Label _statusLabel = new()
    {
        AutoSize = false,
        Dock = DockStyle.Bottom,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(8, 0, 8, 0),
        Height = 22,
        Font = new Font(FontFamily.GenericMonospace, 8.5f),
        ForeColor = SystemColors.GrayText,
        Text = "Click a glyph to type it into the emulator.",
    };

    public FontSheetForm(MZ700 machine)
    {
        _machine = machine;
        Text = "Font Sheet";
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        StartPosition = FormStartPosition.CenterParent;
        AutoScroll = true;
        ShowInTaskbar = false;

        var reload = new Button { Text = "Reload", AutoSize = true, Dock = DockStyle.Top };
        reload.Click += (_, _) => { _machine.Video.InvalidateGlyphCache(); RedrawSheet(); };

        _pic.MouseClick += OnCellClick;

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        scroll.Controls.Add(_pic);

        Controls.Add(scroll);
        Controls.Add(_statusLabel);
        Controls.Add(reload);
        ClientSize = new Size(
            MarginPx + CellsPerRow * CellPx + 24,
            MarginPx * 2 + RowsPerBank * 2 * CellPx + BankGapPx + reload.PreferredSize.Height + _statusLabel.Height + 16);

        RedrawSheet();
    }

    private void RedrawSheet()
    {
        int width = MarginPx + CellsPerRow * CellPx;
        int height = Bank1Y + RowsPerBank * CellPx;
        var bmp = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bmp))
        using (var hdrFont = new Font(FontFamily.GenericMonospace, 7f))
        using (var hdrBrush = new SolidBrush(Color.DimGray))
        {
            g.Clear(Color.White);
            DrawBank(g, bank: 0, yOffset: Bank0Y, hdrFont, hdrBrush);
            DrawBank(g, bank: 1, yOffset: Bank1Y, hdrFont, hdrBrush);
        }
        var old = _pic.Image;
        _pic.Image = bmp;
        old?.Dispose();
    }

    private void DrawBank(Graphics g, int bank, int yOffset, Font hdrFont, Brush hdrBrush)
    {
        for (int c = 0; c < CellsPerRow; c++)
            g.DrawString($"{c:X}", hdrFont, hdrBrush, MarginPx + c * CellPx + 2, yOffset - 14);
        g.DrawString($"Bank {bank}", hdrFont, hdrBrush, 2, yOffset - 14);

        using var reachablePen = new Pen(Color.MediumSeaGreen, 1f);
        for (int r = 0; r < RowsPerBank; r++)
        {
            int cellY = yOffset + r * CellPx;
            g.DrawString($"{r:X}_", hdrFont, hdrBrush, 2, cellY + CellPx / 2 - 6);
            for (int c = 0; c < CellsPerRow; c++)
            {
                byte code = (byte)(r * CellsPerRow + c);
                int cellX = MarginPx + c * CellPx;
                var glyph = _machine.Video.GetGlyph(code, bank, GlyphScale);
                g.DrawImageUnscaled(glyph, cellX, cellY);
                // Outline cells the keyboard can produce in this bank,
                // per RomKeyTables. Helps the user pick a cell that will
                // actually type without having to guess.
                if (_machine.KeyTables.FindByDisplayCode(code, bank) != null)
                    g.DrawRectangle(reachablePen, cellX, cellY, CellPx - 1, CellPx - 1);
            }
        }
    }

    private void OnCellClick(object? sender, MouseEventArgs e)
    {
        if (!TryHitTest(e.X, e.Y, out byte code, out int bank))
        {
            _statusLabel.Text = "Click a glyph cell to type it.";
            return;
        }
        var slot = _machine.KeyTables.FindByDisplayCode(code, bank);
        if (slot is null)
        {
            _statusLabel.Text = $"Bank {bank} code ${code:X2} isn't reachable from the keyboard.";
            return;
        }

        // Mode check: clicking an ALPHA cell while the machine is in
        // GRAPH mode (or vice versa) would press a slot whose lookup is
        // mode-dependent and produce the wrong glyph. Refuse rather than
        // silently mistype.
        bool inGraphMode = (_machine.Mem.Read(0x0060) & 0x10) != 0;
        bool wantGraphMode = bank == RomKeyTables.GraphBank;
        if (inGraphMode != wantGraphMode)
        {
            string need = wantGraphMode ? "GRAPH" : "ALPHA";
            _statusLabel.Text =
                $"Switch the machine to {need} mode first (press the {need} key) to type this glyph.";
            return;
        }

        // GRAPH-bank click-to-type is parked — see docs/usage/keyboard.md.
        // The matrix press lands the right byte in VRAM but BASIC's input
        // handler doesn't set the attribute byte to bank 1 via this path,
        // so the byte renders as the bank-0 glyph at that code instead of
        // the intended graphic. Investigation 2026-06-07 narrowed it to
        // the auto-typer-vs-PC-keystroke difference without finding the
        // missing state. Outline stays so the reachable set is visible,
        // but clicking is suppressed until the attribute write is solved.
        if (bank == RomKeyTables.GraphBank)
        {
            _statusLabel.Text =
                "Bank 1 click-to-type is a known limitation — the glyph lands "
                + "but with the wrong attribute. See docs/usage/keyboard.md.";
            return;
        }

        _machine.Keyboard.TypePress(new CharMap.Press(slot.Value.Row, slot.Value.Col, slot.Value.MzShift));
        string shiftTxt = slot.Value.MzShift ? "shift+" : "";
        _statusLabel.Text =
            $"Typed bank {bank} code ${code:X2} via {shiftTxt}({slot.Value.Row},{slot.Value.Col}).";
    }

    private static bool TryHitTest(int x, int y, out byte code, out int bank)
    {
        code = 0;
        bank = 0;
        int cx = x - MarginPx;
        if (cx < 0 || cx >= CellsPerRow * CellPx) return false;
        int col = cx / CellPx;

        int cellY;
        if (y >= Bank0Y && y < Bank0Y + RowsPerBank * CellPx)
        {
            bank = 0;
            cellY = y - Bank0Y;
        }
        else if (y >= Bank1Y && y < Bank1Y + RowsPerBank * CellPx)
        {
            bank = 1;
            cellY = y - Bank1Y;
        }
        else return false;

        int row = cellY / CellPx;
        code = (byte)(row * CellsPerRow + col);
        return true;
    }
}
