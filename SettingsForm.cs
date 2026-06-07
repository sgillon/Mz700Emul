using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using MZ700Emul.Hardware;

namespace MZ700Emul;

/// <summary>
/// Tabbed settings dialog (Ctrl+,) — the user-facing front end for the
/// values that live in <c>settings.ini</c>. Three tabs at launch:
/// Display, ROMs, Joystick.
///
/// Pattern: the form takes a <see cref="Settings"/> instance, populates
/// its controls from it, and on OK / Apply writes any changes back to
/// the same instance, persists via <see cref="Settings.Save"/> and
/// raises <see cref="Applied"/>. <see cref="MainForm"/> subscribes to
/// <see cref="Applied"/> to push live changes (display scale, joystick
/// button bindings) into the running emulator; ROM-path changes are
/// next picked up on the following reset.
///
/// Hardware reference images embedded under <c>_hardwareimages\</c> are
/// loaded by name (see <see cref="LoadEmbeddedImage"/>). Tabs that have
/// an image render a 200 px image column on the right; tabs without one
/// render full-width.
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly Settings _settings;
    private readonly JoystickInput? _joystickInput;
    private readonly MZ700? _machine;

    // Keyboard tab — diagram is the primary view (P2-7); matrix grid
    // lives behind an Advanced expander.
    private MzKeyboardDiagram? _kbdDiagram;
    private Button? _advancedKbdBtn;
    private Panel? _advancedKbdPanel;
    private KeyboardMatrixGrid? _kbdGrid;
    private System.Windows.Forms.Timer? _kbdGridTimer;
    private ListView? _overridesList;

    // Display
    private readonly RadioButton _rb1x = new() { Text = "&1× (320×200)", AutoSize = true };
    private readonly RadioButton _rb2x = new() { Text = "&2× (640×400)", AutoSize = true };
    private readonly RadioButton _rb3x = new() { Text = "&3× (960×600)", AutoSize = true };

    // ROMs
    private readonly TextBox _txtMonitor = new() { Width = 280 };
    private readonly TextBox _txtFont = new() { Width = 280 };
    private readonly TextBox _txtBasic = new() { Width = 280 };
    private readonly Label _lblMonitorStatus = new() { AutoSize = true };
    private readonly Label _lblFontStatus = new() { AutoSize = true };
    private readonly Label _lblBasicStatus = new() { AutoSize = true };

    // Joystick
    private readonly NumericUpDown _numButton1 = new() { Minimum = 0, Maximum = 31, Width = 60 };
    private readonly NumericUpDown _numButton2 = new() { Minimum = 0, Maximum = 31, Width = 60 };

    /// <summary>Raised after settings are written. MainForm uses this to
    /// reflect changes in the running emulator (display scale, joystick
    /// button bindings).</summary>
    public event Action? Applied;

    public SettingsForm(Settings settings, JoystickInput? joystickInput = null, MZ700? machine = null)
    {
        _settings = settings;
        _joystickInput = joystickInput;
        _machine = machine;
        Text = "Settings";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        // Grew (was 640×380) to fit the 10×8 matrix on the Keyboard tab
        // plus the overrides list below it. Other tabs are short and
        // gain whitespace — acceptable trade. Sized so the matrix
        // (678 tall) plus a ~150 px overrides list plus button row and
        // tab chrome all land inside the form.
        ClientSize = new Size(740, 920);
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        KeyPreview = true;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildDisplayTab());
        tabs.TabPages.Add(BuildRomsTab());
        tabs.TabPages.Add(BuildJoystickTab());
        tabs.TabPages.Add(BuildKeyboardTab());

        var buttonRow = BuildButtonRow();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(6),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        root.Controls.Add(tabs, 0, 0);
        root.Controls.Add(buttonRow, 0, 1);
        Controls.Add(root);

        LoadFromSettings();
        WireValidation();
    }

    // -- Tab construction -----------------------------------------------

    private TabPage BuildDisplayTab()
    {
        var stack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            WrapContents = false,
        };
        stack.Controls.Add(new Label
        {
            Text = "Window scale:",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6),
        });
        stack.Controls.Add(_rb1x);
        stack.Controls.Add(_rb2x);
        stack.Controls.Add(_rb3x);
        return BuildTabPage("Display", stack, image: null);
    }

    private TabPage BuildRomsTab()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 4,
            Padding = new Padding(12),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90f));
        for (int i = 0; i < 4; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));

        AddRomRow(grid, 0, "Monitor ROM:", _txtMonitor, _lblMonitorStatus,
            "Select monitor ROM (1z-013a.rom)", "ROM files (*.rom;*.bin)|*.rom;*.bin|All files|*.*");
        AddRomRow(grid, 1, "Font ROM:", _txtFont, _lblFontStatus,
            "Select character ROM (mz700fon.int)", "Font files (*.int;*.bin;*.txt)|*.int;*.bin;*.txt|All files|*.*");
        AddRomRow(grid, 2, "BASIC:", _txtBasic, _lblBasicStatus,
            "Select S-BASIC cassette (1Z-013B.mzf)", "Cassette files (*.mzf;*.m12;*.mzt)|*.mzf;*.m12;*.mzt|All files|*.*");

        var hint = new Label
        {
            Text = "Monitor/Font path changes take effect on next launch.\nBASIC path takes effect on next Load BASIC.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
        };
        grid.Controls.Add(hint, 0, 3);
        grid.SetColumnSpan(hint, 4);

        return BuildTabPage("ROMs", grid, image: null);
    }

    private void AddRomRow(TableLayoutPanel grid, int row, string label, TextBox textBox, Label statusLabel,
        string browseTitle, string browseFilter)
    {
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 0, 0) }, 0, row);
        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(0, 4, 6, 4);
        grid.Controls.Add(textBox, 1, row);
        statusLabel.Anchor = AnchorStyles.Left;
        statusLabel.Margin = new Padding(0, 8, 0, 0);
        grid.Controls.Add(statusLabel, 2, row);
        var browse = new Button { Text = "Browse…", Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) };
        browse.Click += (_, _) => BrowseFor(textBox, browseTitle, browseFilter);
        grid.Controls.Add(browse, 3, row);
    }

    private TabPage BuildJoystickTab()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
            Padding = new Padding(12),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110f));
        for (int i = 0; i < 4; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));

        var header = new Label
        {
            Text = "PC gamepad button → MZ-1X03 stick",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
        };
        grid.Controls.Add(header, 0, 0);
        grid.SetColumnSpan(header, 3);

        AddJoystickRow(grid, 1, "Left button (SW1):", _numButton1);
        AddJoystickRow(grid, 2, "Right button (SW2):", _numButton2);

        var hint = new Label
        {
            Text = "Click Capture… then press a button on your controller.\nChanges take effect on Apply / OK.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 12, 0, 0),
        };
        grid.Controls.Add(hint, 0, 3);
        grid.SetColumnSpan(hint, 3);

        return BuildTabPage("Joystick", grid, image: LoadEmbeddedImage("MZ-1X03.png"));
    }

    private void AddJoystickRow(TableLayoutPanel grid, int row, string label, NumericUpDown spinner)
    {
        grid.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 0, 0),
        }, 0, row);
        spinner.Margin = new Padding(0, 2, 6, 2);
        grid.Controls.Add(spinner, 1, row);
        var capture = new Button
        {
            Text = "Capture…",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 0, 2),
            Enabled = _joystickInput != null,
        };
        capture.Click += (_, _) => CaptureButtonFor(spinner);
        grid.Controls.Add(capture, 2, row);
    }

    private TabPage BuildKeyboardTab()
    {
        // P2-7 layout: MzKeyboardDiagram is the primary view (top),
        // matrix grid is hidden behind an Advanced expander, and the
        // overrides list keeps its place at the bottom. AutoScroll on
        // the tab content covers the matrix grid (~678 tall) when the
        // expander is open, since the dialog itself is fixed-size.
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(8),
            AutoScroll = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // caption
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 210f));  // diagram
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // advanced button
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // advanced panel
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // overrides list

        layout.Controls.Add(new Label
        {
            Text = "Click any key on the diagram to edit its PC-keyboard binding. "
                 + "Each cap shows the PC key(s) currently bound to it.",
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 4),
        }, 0, 0);

        _kbdDiagram = new MzKeyboardDiagram { Dock = DockStyle.Fill };
        _kbdDiagram.KeyClicked += OnKeyboardDiagramKeyClicked;
        RefreshKeyboardDiagramLabels();
        layout.Controls.Add(_kbdDiagram, 0, 1);

        _advancedKbdBtn = new Button
        {
            Text = "Advanced — matrix grid ▾",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 8, 0, 4),
        };
        _advancedKbdBtn.FlatAppearance.BorderSize = 0;
        _advancedKbdBtn.Click += (_, _) => ToggleAdvancedKbd();
        layout.Controls.Add(_advancedKbdBtn, 0, 2);

        _advancedKbdPanel = new Panel
        {
            AutoSize = true,
            Visible = false,
        };
        if (_machine != null)
        {
            _kbdGrid = new KeyboardMatrixGrid(_machine)
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Margin = new Padding(0),
            };
            _kbdGrid.CellClicked += OnKeyboardCellClicked;
            _advancedKbdPanel.Controls.Add(_kbdGrid);

            // Tick at 10 Hz so the live highlight tracks if anything
            // happens to assert matrix bits while the dialog is open
            // (auto-typer mid-sequence, etc.). Skip the invalidate when
            // the panel is collapsed — no point repainting an offscreen
            // control.
            _kbdGridTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _kbdGridTimer.Tick += (_, _) =>
            {
                if (_advancedKbdPanel?.Visible == true) _kbdGrid?.Invalidate();
            };
            Load += (_, _) => _kbdGridTimer.Start();
            FormClosed += (_, _) =>
            {
                _kbdGridTimer.Stop();
                _kbdGridTimer.Dispose();
            };
        }
        else
        {
            _advancedKbdPanel.Controls.Add(new Label
            {
                Text = "Matrix grid unavailable — emulator instance not provided.",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
            });
        }
        layout.Controls.Add(_advancedKbdPanel, 0, 3);

        // Overrides panel — header + ListView. Aggregates both layers
        // (CharMap chars + KeyOverride VKs) so the user sees everything
        // currently in effect without opening settings.ini.
        var listPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 8, 0, 0),
        };
        listPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        listPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // header row (label + import/export)
        listPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        // Header row: caption on the left, Export / Import buttons on
        // the right. Three columns — label (AutoSize), spacer (100%),
        // buttons (AutoSize) — so both the label and the button group
        // get their natural width and the spacer eats whatever's left.
        var headerRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            RowCount = 1,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
        };
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        headerRow.Controls.Add(new Label
        {
            Text = "Active overrides (summary):",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        }, 0, 0);

        var ioButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0),
        };
        var exportBtn = new Button { Text = "Export…", Width = 90 };
        var importBtn = new Button { Text = "Import…", Width = 90 };
        exportBtn.Click += (_, _) => OnExportMzKbd();
        importBtn.Click += (_, _) => OnImportMzKbd();
        ioButtons.Controls.Add(exportBtn);
        ioButtons.Controls.Add(importBtn);
        headerRow.Controls.Add(ioButtons, 2, 0);
        listPanel.Controls.Add(headerRow, 0, 0);

        _overridesList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            MultiSelect = false,
            Height = 140,
        };
        _overridesList.Columns.Add("Layer",      80);
        _overridesList.Columns.Add("PC trigger", 160);
        _overridesList.Columns.Add("MZ slot",    80);
        _overridesList.Columns.Add("Shift",      100);
        PopulateOverridesList();
        listPanel.Controls.Add(_overridesList, 0, 1);

        layout.Controls.Add(listPanel, 0, 4);
        return BuildTabPage("Keyboard", layout, image: null);
    }

    private void OnExportMzKbd()
    {
        using var dlg = new SaveFileDialog
        {
            Title = "Export keyboard mapping",
            Filter = KeyboardMapFile.FileFilter,
            DefaultExt = "mzkbd",
            AddExtension = true,
            FileName = "mz700-keyboard.mzkbd",
            InitialDirectory = AppContext.BaseDirectory,
            OverwritePrompt = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            KeyboardMapFile.Save(dlg.FileName,
                _settings.CharMapOverrides, _settings.KeyOverrides);
            MessageBox.Show(this,
                $"Exported {_settings.CharMapOverrides.Count} CharMap and " +
                $"{_settings.KeyOverrides.Count} KeyOverride entries to:\n{dlg.FileName}",
                "Export complete",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Failed to save the file:\n\n{ex.Message}",
                "Export failed",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnImportMzKbd()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Import keyboard mapping",
            Filter = KeyboardMapFile.FileFilter,
            InitialDirectory = AppContext.BaseDirectory,
            CheckFileExists = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        CharMapOverrides importedChars;
        KeyOverride importedVks;
        try
        {
            (importedChars, importedVks) = KeyboardMapFile.Load(dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Failed to read the file:\n\n{ex.Message}",
                "Import failed",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Empty file = nothing actionable; bail with a friendly note
        // rather than silently no-op.
        if (importedChars.Count == 0 && importedVks.Count == 0)
        {
            MessageBox.Show(this,
                "The file didn't contain any overrides to import.",
                "Nothing to import",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Merge / Replace / Cancel via a three-button prompt.
        // Yes = Merge (apply on top of current overrides),
        // No  = Replace (clear current first).
        var choice = MessageBox.Show(this,
            $"Import contains {importedChars.Count} CharMap and " +
            $"{importedVks.Count} KeyOverride entries.\n\n" +
            "Yes  = Merge into current overrides (imported entries win on conflict).\n" +
            "No   = Replace current overrides entirely.\n" +
            "Cancel = abort import.",
            "Import keyboard mapping",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);
        if (choice == DialogResult.Cancel) return;

        if (choice == DialogResult.No)
        {
            _settings.CharMapOverrides.Clear();
            _settings.KeyOverrides.Clear();
        }
        foreach (var kv in importedChars.All)
            _settings.CharMapOverrides.Set(kv.Key, kv.Value);
        foreach (var kv in importedVks.All)
            _settings.KeyOverrides.Set(kv.Key, kv.Value);

        // Refresh the surface controls so the change is visible
        // immediately. Persistence still waits for Apply / OK.
        RefreshKeyboardDiagramLabels();
        _kbdGrid?.RefreshBindings();
        PopulateOverridesList();
    }

    private void ToggleAdvancedKbd()
    {
        if (_advancedKbdPanel == null || _advancedKbdBtn == null) return;
        _advancedKbdPanel.Visible = !_advancedKbdPanel.Visible;
        _advancedKbdBtn.Text = _advancedKbdPanel.Visible
            ? "Advanced — matrix grid ▴"
            : "Advanced — matrix grid ▾";
    }

    private void OnKeyboardDiagramKeyClicked(object? sender, KeyDiagramClickedEventArgs e)
    {
        // Editor mutates the override layers directly — change is live
        // for subsequent emulator keystrokes. Persistence still waits
        // for this dialog's Apply / OK.
        using var editor = new MzKeyEditorForm(
            e.Key, _settings.CharMapOverrides, _settings.KeyOverrides);
        editor.ShowDialog(this);
        RefreshKeyboardDiagramLabels();
        _kbdGrid?.RefreshBindings();
        PopulateOverridesList();
    }

    private void RefreshKeyboardDiagramLabels()
    {
        if (_kbdDiagram == null) return;
        _kbdDiagram.PcKeyLabels = PcKeyIndex.BuildLabelsByMzKey(
            _settings.CharMapOverrides, _settings.KeyOverrides);

        // Recompute the unreachable-essential set so the red outline on
        // affected caps tracks live with edits — Apply's safety gate
        // reads the same set, so what you see on the diagram before
        // Apply is what the confirm dialog will mention.
        var slotShiftLabels = PcKeyIndex.BuildLabelsBySlotShift(
            _settings.CharMapOverrides, _settings.KeyOverrides);
        var unreachable = new HashSet<string>();
        foreach (var k in MzKeyboardLayout.EssentialKeys)
        {
            if (!IsKeyFullyReachable(k, slotShiftLabels))
                unreachable.Add(k.Id);
        }
        _kbdDiagram.UnreachableKeyIds = unreachable.Count > 0 ? unreachable : null;

        _kbdDiagram.RefreshLabels();
    }

    /// <summary>
    /// A character key with both unshifted and shifted glyphs is
    /// "fully reachable" only if both halves can be produced from the
    /// host keyboard — losing just the shifted half (e.g. PC '1'
    /// rebound but Shift+1 still maps to MZ '!') still leaves the
    /// unshifted half unreachable, which the gate must surface.
    ///
    /// Fixed-label keys (CR, GRAPH, ALPHA, CTRL, SHIFT, BREAK, INST,
    /// DEL, cursors) are shift-agnostic — any binding in either shift
    /// state is enough.
    /// </summary>
    private static bool IsKeyFullyReachable(
        MzKeyboardLayout.MzKey k,
        IReadOnlyDictionary<(int row, int col, bool shift), IReadOnlyList<string>> labels)
    {
        if (!k.Row.HasValue || !k.Col.HasValue) return true;
        int row = k.Row.Value, col = k.Col.Value;

        if (!string.IsNullOrEmpty(k.FixedLabel))
            return labels.ContainsKey((row, col, false))
                || labels.ContainsKey((row, col, true));

        bool hasUnshifted = !string.IsNullOrEmpty(k.UnshiftedLabel)
            || MzGlyphCatalog.FindByPrintableSlot(row, col, false).HasValue;
        bool hasShifted = !string.IsNullOrEmpty(k.ShiftedLabel)
            || MzGlyphCatalog.FindByPrintableSlot(row, col, true).HasValue;

        if (hasUnshifted && !labels.ContainsKey((row, col, false))) return false;
        if (hasShifted && !labels.ContainsKey((row, col, true))) return false;
        return true;
    }

    private void OnKeyboardCellClicked(object? sender, CellClickedEventArgs e)
    {
        // Matrix-grid click path (Advanced view) still uses the char-only
        // editor — the matrix grid surfaces individual (row, col, shift)
        // slots, which the char layer is keyed by. Fixed-label slots are
        // routed through the diagram instead.
        using var editor = new KeyBindingEditorForm(
            e.Row, e.Col, e.MzShift, _settings.CharMapOverrides);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        _kbdGrid?.RefreshBindings();
        RefreshKeyboardDiagramLabels();
        PopulateOverridesList();
    }

    private void PopulateOverridesList()
    {
        if (_overridesList == null) return;
        _overridesList.Items.Clear();

        foreach (var kv in _settings.CharMapOverrides.All.OrderBy(k => (int)k.Key))
        {
            var p = kv.Value;
            _overridesList.Items.Add(new ListViewItem(new[]
            {
                "CharMap",
                $"'{kv.Key}' (U+{(int)kv.Key:X4})",
                $"({p.Row},{p.Col})",
                p.MzShift ? "shifted" : "unshifted",
            }));
        }

        foreach (var kv in _settings.KeyOverrides.All.OrderBy(k => k.Key.ToString()))
        {
            var b = kv.Value;
            var shiftLabel = b.MzShift switch
            {
                true  => "shifted",
                false => "unshifted",
                _     => "pass-through",
            };
            _overridesList.Items.Add(new ListViewItem(new[]
            {
                "Key",
                kv.Key.ToString(),
                $"({b.Row},{b.Col})",
                shiftLabel,
            }));
        }

        if (_overridesList.Items.Count == 0)
        {
            var empty = new ListViewItem(new[] { "—", "(no overrides set)", "—", "—" })
            {
                ForeColor = SystemColors.GrayText,
            };
            _overridesList.Items.Add(empty);
        }
    }

    private void CaptureButtonFor(NumericUpDown target)
    {
        if (_joystickInput == null) return;
        using var capture = new JoystickCaptureForm(_joystickInput);
        if (capture.ShowDialog(this) != DialogResult.OK) return;
        int idx = capture.CapturedButtonIndex;
        if (idx >= (int)target.Minimum && idx <= (int)target.Maximum)
            target.Value = idx;
    }

    // -- Tab page with optional right-docked image ----------------------

    private static TabPage BuildTabPage(string text, Control content, Image? image)
    {
        var page = new TabPage(text);
        if (image == null)
        {
            content.Dock = DockStyle.Fill;
            page.Controls.Add(content);
            return page;
        }

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        content.Dock = DockStyle.Fill;
        layout.Controls.Add(content, 0, 0);
        layout.Controls.Add(new PictureBox
        {
            Image = image,
            SizeMode = PictureBoxSizeMode.Zoom,
            Dock = DockStyle.Fill,
            Margin = new Padding(6),
        }, 1, 0);
        page.Controls.Add(layout);
        return page;
    }

    // -- Button row -----------------------------------------------------

    private Panel BuildButtonRow()
    {
        var ok = new Button { Text = "OK", DialogResult = DialogResult.None, Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
        var apply = new Button { Text = "Apply", Width = 80 };
        // OK / Apply both run the safety gate before persisting. OK uses
        // DialogResult.None so a refused gate keeps the dialog open
        // rather than closing as it would with DialogResult.OK.
        ok.Click += (_, _) =>
        {
            if (!ConfirmKeyboardSafetyGate()) return;
            ApplyChanges();
            DialogResult = DialogResult.OK;
            Close();
        };
        apply.Click += (_, _) =>
        {
            if (!ConfirmKeyboardSafetyGate()) return;
            ApplyChanges();
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(4),
        };
        flow.Controls.Add(apply);
        flow.Controls.Add(cancel);
        flow.Controls.Add(ok);
        AcceptButton = ok;
        CancelButton = cancel;
        return flow;
    }

    // -- Load / Apply ---------------------------------------------------

    private void LoadFromSettings()
    {
        switch (_settings.DisplayScale)
        {
            case 1: _rb1x.Checked = true; break;
            case 3: _rb3x.Checked = true; break;
            default: _rb2x.Checked = true; break;
        }
        _txtMonitor.Text = _settings.MonitorRomPath;
        _txtFont.Text = _settings.FontPath;
        _txtBasic.Text = _settings.BasicPath;
        _numButton1.Value = Clamp(_settings.JoyButton1Index, 0, 31);
        _numButton2.Value = Clamp(_settings.JoyButton2Index, 0, 31);
        RefreshAllRomStatus();
    }

    private void ApplyChanges()
    {
        _settings.DisplayScale = _rb3x.Checked ? 3 : _rb1x.Checked ? 1 : 2;
        _settings.MonitorRomPath = _txtMonitor.Text.Trim();
        _settings.FontPath = _txtFont.Text.Trim();
        _settings.BasicPath = _txtBasic.Text.Trim();
        _settings.JoyButton1Index = (int)_numButton1.Value;
        _settings.JoyButton2Index = (int)_numButton2.Value;
        _settings.Save();
        Applied?.Invoke();
    }

    /// <summary>
    /// P2-9 safety gate: if any essential MZ key has no PC binding,
    /// switch to the Keyboard tab, highlight the unreachable caps on
    /// the diagram (already happening live via
    /// <see cref="RefreshKeyboardDiagramLabels"/>), and ask the user to
    /// confirm before saving. Returns true if Apply may proceed.
    /// </summary>
    private bool ConfirmKeyboardSafetyGate()
    {
        var unreachableIds = _kbdDiagram?.UnreachableKeyIds;
        if (unreachableIds == null || unreachableIds.Count == 0) return true;

        var unreachable = MzKeyboardLayout.Keys
            .Where(k => unreachableIds.Contains(k.Id))
            .ToList();

        // Pull the user's attention to the diagram so the red outlines
        // and the dialog text describe the same keys.
        if (_kbdDiagram?.Parent is TabPage page && page.Parent is TabControl tabs)
            tabs.SelectedTab = page;

        const int previewMax = 10;
        var names = string.Join(", ", unreachable.Take(previewMax).Select(DescribeKeyForGate));
        if (unreachable.Count > previewMax)
            names += $", … (+{unreachable.Count - previewMax} more)";

        var result = MessageBox.Show(this,
            $"{unreachable.Count} essential MZ key(s) have no PC binding:\n\n" +
            $"{names}\n\n" +
            "These keys are unreachable from the host keyboard until rebound. " +
            "Apply anyway?",
            "Unreachable keys — Apply anyway?",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        return result == DialogResult.Yes;
    }

    private static string DescribeKeyForGate(MzKeyboardLayout.MzKey k)
    {
        if (!string.IsNullOrEmpty(k.FixedLabel)) return k.FixedLabel!;
        if (!string.IsNullOrEmpty(k.UnshiftedLabel)) return k.UnshiftedLabel!;
        if (!string.IsNullOrEmpty(k.ShiftedLabel)) return k.ShiftedLabel!;
        if (k.Row.HasValue && k.Col.HasValue)
        {
            var c = MzGlyphCatalog.FindByPrintableSlot(k.Row.Value, k.Col.Value, false)
                  ?? MzGlyphCatalog.FindByPrintableSlot(k.Row.Value, k.Col.Value, true);
            if (c.HasValue) return c.Value.ToString();
        }
        return k.Id;
    }

    // -- ROM browse + path-status indicator -----------------------------

    private void WireValidation()
    {
        _txtMonitor.TextChanged += (_, _) => UpdateRomStatus(_txtMonitor, _lblMonitorStatus);
        _txtFont.TextChanged += (_, _) => UpdateRomStatus(_txtFont, _lblFontStatus);
        _txtBasic.TextChanged += (_, _) => UpdateRomStatus(_txtBasic, _lblBasicStatus);
    }

    private void RefreshAllRomStatus()
    {
        UpdateRomStatus(_txtMonitor, _lblMonitorStatus);
        UpdateRomStatus(_txtFont, _lblFontStatus);
        UpdateRomStatus(_txtBasic, _lblBasicStatus);
    }

    private static void UpdateRomStatus(TextBox textBox, Label statusLabel)
    {
        var path = textBox.Text.Trim();
        if (string.IsNullOrEmpty(path))
        {
            statusLabel.Text = "(unset)";
            statusLabel.ForeColor = SystemColors.GrayText;
            return;
        }
        var resolved = Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
        if (File.Exists(resolved))
        {
            statusLabel.Text = "✓ found";
            statusLabel.ForeColor = Color.FromArgb(0, 128, 0);
        }
        else
        {
            statusLabel.Text = "✗ missing";
            statusLabel.ForeColor = Color.FromArgb(192, 0, 0);
        }
    }

    private void BrowseFor(TextBox target, string title, string filter)
    {
        using var dlg = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
        };
        var current = target.Text.Trim();
        if (!string.IsNullOrEmpty(current))
        {
            var resolved = Path.IsPathRooted(current)
                ? current
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, current));
            if (File.Exists(resolved))
            {
                dlg.InitialDirectory = Path.GetDirectoryName(resolved);
                dlg.FileName = Path.GetFileName(resolved);
            }
        }
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        target.Text = MakeRelativeToBase(dlg.FileName);
    }

    private static string MakeRelativeToBase(string absolutePath)
    {
        var baseDir = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(absolutePath);
        var prefix = baseDir + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? full.Substring(prefix.Length)
            : full;
    }

    // -- Embedded image loader ------------------------------------------

    private static Image? LoadEmbeddedImage(string filename)
    {
        try
        {
            var asm = typeof(SettingsForm).Assembly;
            var match = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(filename, StringComparison.OrdinalIgnoreCase));
            if (match == null) return null;
            using var s = asm.GetManifestResourceStream(match);
            if (s == null) return null;
            // Image.FromStream requires the source stream to outlive the
            // Image — copy into a memory stream we keep open implicitly
            // via the Image's internal reference.
            var ms = new MemoryStream();
            s.CopyTo(ms);
            ms.Position = 0;
            return Image.FromStream(ms);
        }
        catch
        {
            return null;
        }
    }

    private static int Clamp(int value, int min, int max) =>
        value < min ? min : value > max ? max : value;
}
