using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using MZ700Emul.Hardware;

namespace MZ700Emul;

/// <summary>
/// Live "what just happened" view of host keyboard + joystick input and
/// the resolved MZ-700 matrix state. Opened from Debug → HID Diagnostic
/// (Ctrl+H); refreshed once per frame by MainForm.Timer_Tick.
///
/// Practical-not-polished: a single monospace label per section, redrawn
/// in full each frame. Allocations are tiny (a few short strings) and
/// SmoothLabel suppresses the WM_ERASEBKGND flash.
/// </summary>
public sealed class HidDiagnosticForm : Form
{
    private readonly MZ700 _machine;
    private readonly JoystickInput _joystick;

    private readonly SmoothLabel _hostLabel = AutoSizeMonoLabel();
    private readonly SmoothLabel _mappingLabel = AutoSizeMonoLabel();
    private readonly SmoothLabel _mzLabel = FillMonoLabel();
    private readonly Label _statusLabel = new()
    {
        AutoSize = false,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font(FontFamily.GenericSansSerif, 8.5f),
        ForeColor = SystemColors.GrayText,
    };

    // Keep focus on the main window when this diagnostic is opened —
    // the whole point is to watch the main window's input flow.
    protected override bool ShowWithoutActivation => true;

    public HidDiagnosticForm(MZ700 machine, JoystickInput joystick)
    {
        _machine = machine;
        _joystick = joystick;

        Text = "HID Diagnostic";
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(440, 540);
        MinimumSize = new Size(360, 460);
        ShowInTaskbar = false;
        // Don't steal keystrokes from the main window — the whole point of
        // the diagnostic is to watch the main window's input flow.
        KeyPreview = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(6),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(AutoGroup("Host input (Windows side)", _hostLabel), 0, 0);
        root.Controls.Add(AutoGroup("Mapping (which layer matched)", _mappingLabel), 0, 1);
        root.Controls.Add(FillGroup("MZ-700 side", _mzLabel), 0, 2);
        root.Controls.Add(BuildButtonRow(), 0, 3);
        Controls.Add(root);
    }

    private Control BuildButtonRow()
    {
        var copyBtn = new Button { Text = "Copy", AutoSize = true, Margin = new Padding(3) };
        copyBtn.Click += (_, _) => CopyToClipboard();
        var saveBtn = new Button { Text = "Save…", AutoSize = true, Margin = new Padding(3) };
        saveBtn.Click += (_, _) => SaveToFile();

        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 4, 0, 0),
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        row.Controls.Add(_statusLabel, 0, 0);
        row.Controls.Add(copyBtn, 1, 0);
        row.Controls.Add(saveBtn, 2, 0);
        return row;
    }

    private void CopyToClipboard()
    {
        try
        {
            Clipboard.SetText(BuildFullDump());
            _statusLabel.Text = "Copied current view to clipboard.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Copy failed: {ex.Message}";
        }
    }

    private void SaveToFile()
    {
        using var dlg = new SaveFileDialog
        {
            Title = "Save HID diagnostic snapshot",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"hid-diag-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            File.WriteAllText(dlg.FileName, BuildFullDump());
            _statusLabel.Text = $"Saved to {Path.GetFileName(dlg.FileName)}.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Save failed: {ex.Message}";
        }
    }

    private string BuildFullDump()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"MZ700Emul HID Diagnostic — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("=== Host input (Windows side) ===");
        sb.AppendLine(BuildHostText());
        sb.AppendLine("=== Mapping (which layer matched) ===");
        sb.AppendLine(BuildMappingText());
        sb.AppendLine("=== MZ-700 side ===");
        sb.AppendLine(BuildMzText());
        return sb.ToString();
    }

    // AutoSize labels for the AutoSize rows: GroupBox sizes to label,
    // row sizes to GroupBox. Used for the host + mapping panes whose
    // content is a fixed handful of lines.
    private static SmoothLabel AutoSizeMonoLabel() => new()
    {
        AutoSize = true,
        Font = new Font(FontFamily.GenericMonospace, 9f),
        Margin = new Padding(4),
    };

    // Fill label for the Percent(100) row: takes whatever space the
    // user gives by resizing the window. Used for the matrix pane.
    private static SmoothLabel FillMonoLabel() => new()
    {
        Dock = DockStyle.Fill,
        Font = new Font(FontFamily.GenericMonospace, 9f),
        AutoSize = false,
        Padding = new Padding(4),
        TextAlign = ContentAlignment.TopLeft,
    };

    private static GroupBox AutoGroup(string title, Control content)
    {
        var gb = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(6, 16, 6, 6),
        };
        gb.Controls.Add(content);
        return gb;
    }

    private static GroupBox FillGroup(string title, Control content)
    {
        var gb = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(6, 16, 6, 6),
        };
        gb.Controls.Add(content);
        return gb;
    }

    /// <summary>Called once per frame by MainForm's Timer_Tick.</summary>
    public void RefreshIfVisible()
    {
        if (!Visible) return;
        _hostLabel.Text = BuildHostText();
        _mappingLabel.Text = BuildMappingText();
        _mzLabel.Text = BuildMzText();
    }

    private string BuildHostText()
    {
        var d = _machine.Keyboard.Diag;
        var sb = new StringBuilder();
        sb.AppendLine($"Last KeyDown : {FormatKeyData(d.LastKeyDown)}");
        sb.AppendLine($"Last KeyPress: {FormatChar(d.LastKeyChar)}");
        sb.AppendLine($"Last KeyUp   : {FormatKeyData(d.LastKeyUp)}");
        sb.AppendLine($"Mod keys held: {FormatModifiers(Control.ModifierKeys)}");
        sb.AppendLine();
        sb.AppendLine($"Joystick (SW1=button {MaskToIndex(_joystick.Sw1ButtonMask)}, " +
                      $"SW2=button {MaskToIndex(_joystick.Sw2ButtonMask)}):");
        for (uint slot = 0; slot < 2; slot++)
        {
            uint btns = _joystick.GetCurrentButtons(slot);
            var s = _machine.Joystick.Sticks[slot];
            sb.AppendLine($"  Slot {slot}: " +
                (s.Active
                    ? $"X={s.AxisX,3} Y={s.AxisY,3}  buttons=0x{btns:X8}  SW1={Bit(s.Sw1)} SW2={Bit(s.Sw2)}"
                    : "(not connected)"));
        }
        return sb.ToString();
    }

    private static string Bit(bool b) => b ? "1" : "0";

    private static string MaskToIndex(uint mask)
    {
        if (mask == 0) return "-";
        for (int i = 0; i < 32; i++) if (((mask >> i) & 1) == 1) return i.ToString();
        return "-";
    }

    private string BuildMappingText()
    {
        var d = _machine.Keyboard.Diag;
        string shift = d.LastMzShift switch
        {
            true => "shift=ON",
            false => "shift=OFF",
            null => "shift=pass-through",
        };
        string resolved = d.LastRow >= 0
            ? $"(row {d.LastRow}, col {d.LastCol})  {shift}"
            : "(no match)";
        var sb = new StringBuilder();
        sb.AppendLine($"Layer matched: {d.LastLayer}");
        sb.AppendLine($"Resolved     : {resolved}");
        return sb.ToString();
    }

    private string BuildMzText()
    {
        var kb = _machine.Keyboard;
        var sb = new StringBuilder();
        sb.AppendLine("Matrix (active-low; 0 = pressed):");
        sb.AppendLine("       7 6 5 4 3 2 1 0");
        for (int row = 0; row < 10; row++)
        {
            byte b = kb.PeekMatrixRow(row);
            var bits = new StringBuilder(15);
            for (int bit = 7; bit >= 0; bit--)
            {
                bits.Append((b & (1 << bit)) != 0 ? '1' : '0');
                if (bit > 0) bits.Append(' ');
            }
            sb.AppendLine($"Row {row}: {bits}  (${b:X2})");
        }
        sb.AppendLine();
        sb.AppendLine($"Last row scanned by OS: {(kb.Diag.LastScanRow < 0 ? "-" : kb.Diag.LastScanRow.ToString())}");
        byte shiftMirror = _machine.Mem.Read(0x1170);
        sb.AppendLine($"$1170 (shift mirror): ${shiftMirror:X2}");
        byte modeByte = _machine.Mem.Read(0x0060);
        string mode = (modeByte & 0x10) != 0 ? "GRAPH" : "ALPHA";
        sb.AppendLine($"$0060 (mode flag)   : ${modeByte:X2}  ->  {mode}  (only valid once BASIC is loaded)");
        return sb.ToString();
    }

    private static string FormatKeyData(Keys k) =>
        k == Keys.None ? "(none)" : $"{k}  (0x{(int)k:X4})";

    private static string FormatChar(char ch)
    {
        if (ch == '\0') return "(none)";
        return ch >= 0x20 && ch < 0x7F
            ? $"'{ch}'  (0x{(int)ch:X2})"
            : $"0x{(int)ch:X2}";
    }

    private static string FormatModifiers(Keys mods)
    {
        if (mods == Keys.None) return "(none)";
        var parts = new System.Collections.Generic.List<string>();
        if ((mods & Keys.Shift) != 0) parts.Add("Shift");
        if ((mods & Keys.Control) != 0) parts.Add("Control");
        if ((mods & Keys.Alt) != 0) parts.Add("Alt");
        return string.Join(", ", parts);
    }
}
