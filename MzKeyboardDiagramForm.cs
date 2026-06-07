using System.Drawing;
using System.Windows.Forms;
using MZ700Emul.Hardware;

namespace MZ700Emul;

/// <summary>
/// Temporary host for <see cref="MzKeyboardDiagram"/> during Phase 2 of
/// the keyboard-editor redesign. Once the diagram is wired into Settings
/// → Keyboard at P2-7, this form goes away (it stays for now as a quick
/// look at the layout without opening Settings, and to verify the layout
/// spec against the Owner's Manual).
/// </summary>
public sealed class MzKeyboardDiagramForm : Form
{
    private readonly MzKeyboardDiagram _diagram;
    private CharMapOverrides? _charOverrides;
    private KeyOverride? _keyOverrides;

    protected override bool ShowWithoutActivation => true;

    public MzKeyboardDiagramForm()
    {
        Text = "MZ Keyboard Diagram (preview)";
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        ClientSize = new Size(800, 360);
        MinimumSize = new Size(360, 200);

        _diagram = new MzKeyboardDiagram
        {
            Dock = DockStyle.Fill,
        };
        _diagram.KeyClicked += OnDiagramKeyClicked;
        Controls.Add(_diagram);
    }

    /// <summary>
    /// Recompute the per-key PC binding labels from the supplied override
    /// layers and push them onto the diagram. Called on each open so
    /// labels stay in sync with settings.ini edits between sessions.
    /// </summary>
    public void SetLabels(CharMapOverrides? charOverrides, KeyOverride? keyOverrides)
    {
        _charOverrides = charOverrides;
        _keyOverrides = keyOverrides;
        _diagram.PcKeyLabels = PcKeyIndex.BuildLabelsByMzKey(charOverrides, keyOverrides);
        _diagram.RefreshLabels();
    }

    private void OnDiagramKeyClicked(object? sender, KeyDiagramClickedEventArgs e)
    {
        if (_charOverrides == null) return; // labels not initialised yet
        using var editor = new MzKeyEditorForm(e.Key, _charOverrides, _keyOverrides);
        editor.ShowDialog(this);
        // Refresh diagram overlay so Edit / Reset effects are visible.
        SetLabels(_charOverrides, _keyOverrides);
    }
}
