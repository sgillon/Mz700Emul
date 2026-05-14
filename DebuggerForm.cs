using System;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using MZ700Emul.Z80;

namespace MZ700Emul;

/// <summary>
/// Phase 1 debugger window: CPU execution control (pause / resume /
/// single-step / step-frame), a live Z80 register view, and a simple
/// address-based breakpoint manager.
///
/// Opened from MainForm's Debug menu; MainForm pumps
/// <see cref="RefreshIfVisible"/> once per frame. Everything runs on the
/// WinForms UI thread — "paused" means <see cref="MZ700.RunFrame"/>
/// early-returns, never that a thread is blocked, so the window stays
/// responsive.
///
/// The whole window is laid out inside a single root TableLayoutPanel
/// (2 columns × 3 rows) so there is no Dock z-order ambiguity.
/// </summary>
public sealed class DebuggerForm : Form
{
    private readonly MZ700 _machine;
    private readonly Action _resetMachine;

    private readonly Button _btnPause = new();
    private readonly Button _btnStep = new();
    private readonly Button _btnStepFrame = new();
    private readonly Button _btnReset = new();
    private readonly Label _regLabel = new();
    private readonly TextBox _bpAddr = new();
    private readonly Button _bpAdd = new();
    private readonly Button _bpRemove = new();
    private readonly ListBox _bpList = new();
    private readonly Label _statusLabel = new();

    public DebuggerForm(MZ700 machine, Action resetMachine)
    {
        _machine = machine;
        _resetMachine = resetMachine;

        Text = "Debugger";
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        ClientSize = new Size(520, 360);
        MinimumSize = new Size(460, 340);
        KeyPreview = true;
        DoubleBuffered = true;
        ShowInTaskbar = false;

        var mono = new Font(FontFamily.GenericMonospace, 9f);

        // ===== root layout: 2 columns × 3 rows =====
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240f));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));   // button bar
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // reg view | breakpoints
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));   // status line

        // --- button bar (row 0, spans both columns) ---
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(2),
        };
        ConfigButton(_btnPause, "Pause (F5)", 104, (_, _) => TogglePause());
        ConfigButton(_btnStep, "Step (F10)", 88, (_, _) => DoStep());
        ConfigButton(_btnStepFrame, "Step Frame (F11)", 122, (_, _) => DoStepFrame());
        ConfigButton(_btnReset, "Reset", 64, (_, _) => DoReset());
        buttonPanel.Controls.AddRange(new Control[] { _btnPause, _btnStep, _btnStepFrame, _btnReset });
        root.Controls.Add(buttonPanel, 0, 0);
        root.SetColumnSpan(buttonPanel, 2);

        // --- register view (row 1, column 0) ---
        _regLabel.Dock = DockStyle.Fill;
        _regLabel.AutoSize = false;
        _regLabel.Font = mono;
        _regLabel.BackColor = Color.White;
        _regLabel.BorderStyle = BorderStyle.FixedSingle;
        _regLabel.TextAlign = ContentAlignment.TopLeft;
        _regLabel.Padding = new Padding(6);
        _regLabel.UseMnemonic = false;
        root.Controls.Add(_regLabel, 0, 1);

        // --- breakpoint manager (row 1, column 1) ---
        var bpPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        bpPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        bpPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));   // input row
        bpPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // list

        var bpInput = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(2),
        };
        var bpLabel = new Label
        {
            Text = "Addr $",
            AutoSize = true,
            Margin = new Padding(2, 7, 0, 0),
        };
        _bpAddr.Width = 60;
        _bpAddr.Font = mono;
        _bpAddr.MaxLength = 5;
        _bpAddr.Margin = new Padding(2, 4, 4, 0);
        ConfigButton(_bpAdd, "Add", 52, (_, _) => AddBreakpoint());
        ConfigButton(_bpRemove, "Remove", 64, (_, _) => RemoveSelectedBreakpoint());
        bpInput.Controls.AddRange(new Control[] { bpLabel, _bpAddr, _bpAdd, _bpRemove });
        bpPanel.Controls.Add(bpInput, 0, 0);

        _bpList.Dock = DockStyle.Fill;
        _bpList.Font = mono;
        _bpList.IntegralHeight = false;
        _bpList.DoubleClick += (_, _) => RemoveSelectedBreakpoint();
        bpPanel.Controls.Add(_bpList, 0, 1);

        root.Controls.Add(bpPanel, 1, 1);

        // --- status line (row 2, spans both columns) ---
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.AutoSize = false;
        _statusLabel.BorderStyle = BorderStyle.Fixed3D;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Padding = new Padding(4, 0, 0, 0);
        root.Controls.Add(_statusLabel, 0, 2);
        root.SetColumnSpan(_statusLabel, 2);

        Controls.Add(root);

        FormClosing += OnFormClosing;
        UpdateAll();
    }

    private static void ConfigButton(Button b, string text, int width, EventHandler onClick)
    {
        b.Text = text;
        b.Width = width;
        b.Height = 28;
        b.TabStop = false;
        b.Click += onClick;
    }

    /// <summary>
    /// F5 = pause/resume, F10 = single-step, F11 = step-frame. Uses
    /// ProcessCmdKey so the function keys are caught reliably (F10 would
    /// otherwise be swallowed as a menu-activation key).
    /// </summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.F5: TogglePause(); return true;
            case Keys.F10: DoStep(); return true;
            case Keys.F11: DoStepFrame(); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // --- execution control ----------------------------------------------

    private void TogglePause()
    {
        if (_machine.Paused) _machine.Resume();
        else _machine.Pause();
        UpdateAll();
    }

    private void DoStep()
    {
        _machine.StepInstruction();   // also leaves the machine paused
        UpdateAll();
    }

    private void DoStepFrame()
    {
        _machine.Pause();
        _machine.StepFrame();         // honoured by the next Timer_Tick
        UpdateAll();
    }

    private void DoReset()
    {
        _resetMachine();
        UpdateAll();
    }

    // --- breakpoints ----------------------------------------------------

    private void AddBreakpoint()
    {
        if (TryParseAddr(_bpAddr.Text, out ushort addr))
        {
            _machine.Cpu.Breakpoints[addr] = true;
            _bpAddr.Clear();
            UpdateBreakpointList();
        }
        else
        {
            _statusLabel.Text = "Invalid address — enter a hex value 0–FFFF.";
        }
    }

    private void RemoveSelectedBreakpoint()
    {
        if (_bpList.SelectedItem is string sel && TryParseAddr(sel, out ushort addr))
        {
            _machine.Cpu.Breakpoints[addr] = false;
            UpdateBreakpointList();
        }
    }

    private static bool TryParseAddr(string s, out ushort addr)
    {
        addr = 0;
        s = s.Trim();
        if (s.StartsWith("$", StringComparison.Ordinal)) s = s[1..];
        else if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        if (s.Length == 0) return false;
        if (!int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return false;
        if (v < 0 || v > 0xFFFF) return false;
        addr = (ushort)v;
        return true;
    }

    // --- refresh --------------------------------------------------------

    /// <summary>Called once per frame by MainForm's Timer_Tick.</summary>
    public void RefreshIfVisible()
    {
        if (!Visible) return;
        UpdateButtons();
        UpdateRegisters();
        UpdateStatus();
    }

    private void UpdateAll()
    {
        UpdateButtons();
        UpdateRegisters();
        UpdateStatus();
        UpdateBreakpointList();
    }

    private void UpdateButtons()
    {
        _btnPause.Text = _machine.Paused ? "Resume (F5)" : "Pause (F5)";
    }

    private void UpdateRegisters()
    {
        var c = _machine.Cpu;
        var sb = new StringBuilder();
        sb.AppendLine($"PC = {c.PC:X4}    SP = {c.SP:X4}");
        sb.AppendLine();
        sb.AppendLine($"AF = {c.AF:X4}    AF'= {c.AF_:X4}");
        sb.AppendLine($"BC = {c.BC:X4}    BC'= {c.BC_:X4}");
        sb.AppendLine($"DE = {c.DE:X4}    DE'= {c.DE_:X4}");
        sb.AppendLine($"HL = {c.HL:X4}    HL'= {c.HL_:X4}");
        sb.AppendLine($"IX = {c.IX:X4}    IY = {c.IY:X4}");
        sb.AppendLine();
        sb.AppendLine($"I = {c.I:X2}  R = {c.R:X2}   IM = {c.IM}");
        sb.AppendLine($"IFF1 = {(c.IFF1 ? 1 : 0)}  IFF2 = {(c.IFF2 ? 1 : 0)}  HALT = {(c.Halted ? 1 : 0)}");
        sb.AppendLine();
        sb.AppendLine($"Flags  {DecodeFlags(c.F)}");
        sb.AppendLine();
        sb.Append($"Cycles {c.TotalCycles:N0}");
        _regLabel.Text = sb.ToString();
    }

    private static string DecodeFlags(byte f)
    {
        // Shown as the letter when set, '·' when clear: S Z H P/V N C.
        char Bit(byte mask, char letter) => (f & mask) != 0 ? letter : '·';
        return $"{Bit(Z80Cpu.FLAG_S, 'S')} {Bit(Z80Cpu.FLAG_Z, 'Z')} " +
               $"{Bit(Z80Cpu.FLAG_H, 'H')} {Bit(Z80Cpu.FLAG_PV, 'P')} " +
               $"{Bit(Z80Cpu.FLAG_N, 'N')} {Bit(Z80Cpu.FLAG_C, 'C')}";
    }

    private void UpdateStatus()
    {
        var c = _machine.Cpu;
        if (_machine.Paused && c.BreakpointTripped)
            _statusLabel.Text = $"Stopped — breakpoint at ${c.PC:X4}";
        else if (_machine.Paused)
            _statusLabel.Text = $"Paused at ${c.PC:X4}";
        else
            _statusLabel.Text = "Running";
    }

    private void UpdateBreakpointList()
    {
        _bpList.BeginUpdate();
        _bpList.Items.Clear();
        var bps = _machine.Cpu.Breakpoints;
        for (int a = 0; a < bps.Length; a++)
            if (bps[a]) _bpList.Items.Add($"${a:X4}");
        _bpList.EndUpdate();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // Closing the window just hides it — keep breakpoints and the
        // instance alive so reopening is instant. MainForm disposes it
        // for real when the emulator shuts down.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
