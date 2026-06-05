using System;
using System.Drawing;
using System.Windows.Forms;
using MZ700Emul.Hardware;

namespace MZ700Emul;

/// <summary>
/// Modal editor that binds a PC keystroke to a single MZ-700 matrix slot.
/// Phase A: writes only into the <see cref="CharMapOverrides"/> layer —
/// captures that resolve to a Unicode char are saved; non-character VKs
/// (modifiers, function keys, cursors, Enter, Esc, Tab) are politely
/// refused with a "Phase B coming" note.
///
/// The target slot is fixed at construction (cell-clicked in the matrix
/// grid). The MzShift checkbox under the Advanced expander lets the user
/// flip the shift assertion away from the default the cell-click implied.
///
/// Mutations are live: <see cref="CharMapOverrides.Set"/> is called from
/// Save and immediately affects subsequent keystrokes. Persistence to
/// <c>settings.ini</c> still waits for the parent <see cref="SettingsForm"/>'s
/// Apply / OK — consistent with the rest of the dialog.
/// </summary>
public sealed class KeyBindingEditorForm : Form
{
    public int Row { get; }
    public int Col { get; }
    public bool MzShift => _shiftCheck.Checked;

    private readonly CharMapOverrides _overrides;
    private readonly KeyCaptureControl _capture;
    private readonly Label _status;
    private readonly Label _phaseBNote;
    private readonly Button _advancedBtn;
    private readonly Panel _advancedPanel;
    private readonly CheckBox _shiftCheck;
    private readonly Button _saveBtn;

    private char? _capturedChar;

    public KeyBindingEditorForm(int row, int col, bool defaultMzShift, CharMapOverrides overrides)
    {
        Row = row;
        Col = col;
        _overrides = overrides;

        Text = "Bind PC key to MZ slot";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(460, 360);
        KeyPreview = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(12),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        // header / capture / status / phaseB / advanced btn / advanced panel / buttons
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Header — describes the target slot.
        var header = new Label
        {
            Text = BuildHeaderText(row, col, defaultMzShift),
            AutoSize = true,
            Font = new Font(Font.FontFamily, 10f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8),
        };
        root.Controls.Add(header, 0, 0);

        // Capture control.
        _capture = new KeyCaptureControl
        {
            Dock = DockStyle.Top,
            Height = 110,
            Margin = new Padding(0, 0, 0, 8),
        };
        _capture.Captured += OnCaptured;
        root.Controls.Add(_capture, 0, 1);

        // Status line — shows what's about to be bound on a successful capture.
        _status = new Label
        {
            Text = "",
            AutoSize = true,
            ForeColor = SystemColors.ControlText,
            Margin = new Padding(0, 0, 0, 4),
        };
        root.Controls.Add(_status, 0, 2);

        // Phase-B note — visible only when a non-char VK is captured.
        _phaseBNote = new Label
        {
            Text = "",
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            ForeColor = Color.DarkOrange,
            Visible = false,
            Margin = new Padding(0, 0, 0, 4),
        };
        root.Controls.Add(_phaseBNote, 0, 3);

        // Advanced expander button.
        _advancedBtn = new Button
        {
            Text = "Advanced ▾",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 8, 0, 0),
        };
        _advancedBtn.FlatAppearance.BorderSize = 0;
        _advancedBtn.Click += (_, _) => ToggleAdvanced();
        root.Controls.Add(_advancedBtn, 0, 4);

        // Advanced panel — hidden by default. MzShift checkbox lives here.
        _shiftCheck = new CheckBox
        {
            Text = "Assert MZ shift while this key is held",
            Checked = defaultMzShift,
            AutoSize = true,
        };
        _advancedPanel = new Panel
        {
            AutoSize = true,
            Visible = false,
            Margin = new Padding(16, 4, 0, 0),
        };
        _advancedPanel.Controls.Add(_shiftCheck);
        root.Controls.Add(_advancedPanel, 0, 5);

        // Buttons.
        _saveBtn = new Button { Text = "Save", Width = 80, Enabled = false };
        var cancelBtn = new Button { Text = "Cancel", Width = 80, DialogResult = DialogResult.Cancel };
        _saveBtn.Click += (_, _) => OnSave();
        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0),
        };
        buttonRow.Controls.Add(cancelBtn);
        buttonRow.Controls.Add(_saveBtn);
        root.Controls.Add(buttonRow, 0, 6);

        AcceptButton = _saveBtn;
        CancelButton = cancelBtn;

        Controls.Add(root);
        Shown += (_, _) => _capture.Focus();
    }

    private static string BuildHeaderText(int row, int col, bool mzShift)
    {
        var un = MzGlyphCatalog.FindByPrintableSlot(row, col, false);
        var sh = MzGlyphCatalog.FindByPrintableSlot(row, col, true);
        char? targetGlyph = mzShift ? sh : un;
        var special = MzGlyphCatalog.FindSpecialLabel(row, col);

        var shiftLabel = mzShift ? "shifted" : "unshifted";
        if (targetGlyph.HasValue)
            return $"Target: MZ slot ({row},{col}) producing '{targetGlyph.Value}' ({shiftLabel})";
        if (special != null)
            return $"Target: MZ slot ({row},{col}) — {special} ({shiftLabel})";
        return $"Target: MZ slot ({row},{col}) — no printable glyph ({shiftLabel})";
    }

    private void ToggleAdvanced()
    {
        _advancedPanel.Visible = !_advancedPanel.Visible;
        _advancedBtn.Text = _advancedPanel.Visible ? "Advanced ▴" : "Advanced ▾";
    }

    private void OnCaptured(object? sender, KeyCapturedEventArgs e)
    {
        _capturedChar = e.Char;

        if (e.Char.HasValue)
        {
            var ch = e.Char.Value;
            var existing = "";
            if (_overrides.TryLookup(ch, out var cur))
                existing = $" — currently overridden to ({cur.Row},{cur.Col}) {(cur.MzShift ? "shifted" : "unshifted")}";
            else if (CharMap.Defaults.TryGetValue(ch, out var def))
                existing = $" — default maps to ({def.Row},{def.Col}) {(def.MzShift ? "shifted" : "unshifted")}";
            else
                existing = " — no existing binding";

            _status.Text = $"Will bind '{ch}' (U+{(int)ch:X4}) → ({Row},{Col}){existing}.";
            _status.ForeColor = SystemColors.ControlText;
            _phaseBNote.Visible = false;
            _saveBtn.Enabled = true;
        }
        else
        {
            _status.Text = "";
            _phaseBNote.Text =
                "Non-character keys (modifiers, function keys, cursors, Enter, Esc, Tab) edit " +
                "the Key Overrides layer, which arrives in Phase B. For now, hand-edit the " +
                "[KeyOverrides] section in settings.ini.";
            _phaseBNote.Visible = true;
            _saveBtn.Enabled = false;
        }
    }

    private void OnSave()
    {
        if (_capturedChar is not char ch) return;
        _overrides.Set(ch, new CharMap.Press(Row, Col, _shiftCheck.Checked));
        DialogResult = DialogResult.OK;
        Close();
    }
}
