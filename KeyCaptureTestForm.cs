using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MZ700Emul;

/// <summary>
/// Temporary host for <see cref="KeyCaptureControl"/> — used during
/// Phase A of the keyboard-editor build to exercise the capture surface
/// in isolation before it's slotted into <c>KeyBindingEditorForm</c>.
/// Triggered from Debug → Key Capture Test… and will be removed once the
/// real editor lands.
/// </summary>
public sealed class KeyCaptureTestForm : Form
{
    private readonly KeyCaptureControl _capture;
    private readonly Label _details;

    public KeyCaptureTestForm()
    {
        Text = "Key Capture Test";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(480, 320);

        // Deliberately leave KeyPreview off and AcceptButton/CancelButton
        // unset: the capture control needs Enter and Esc to land in its
        // own OnKeyDown rather than triggering the form's default-button
        // / cancel-button logic.
        KeyPreview = false;

        _capture = new KeyCaptureControl
        {
            Dock = DockStyle.Top,
            Height = 140,
        };
        _capture.Captured += OnCaptured;

        _details = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(10),
            Font = new Font(FontFamily.GenericMonospace, 9f),
            Text = "Click the box above, then press any key.\r\n" +
                   "Tab / Esc / Enter / arrows are all captured.\r\n" +
                   "Hold modifiers (Ctrl/Shift/Alt) and press a key to capture the combination.",
        };

        var bottomBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8, 4, 8, 4),
        };
        var btnClose = new Button { Text = "Close", AutoSize = true };
        btnClose.Click += (_, _) => Close();
        var btnReset = new Button { Text = "Reset", AutoSize = true };
        btnReset.Click += (_, _) => { _capture.ResetCapture(); _details.Text = "Capture cleared."; _capture.Focus(); };
        bottomBar.Controls.Add(btnClose);
        bottomBar.Controls.Add(btnReset);

        Controls.Add(_details);
        Controls.Add(bottomBar);
        Controls.Add(_capture);

        Shown += (_, _) => _capture.Focus();
    }

    private void OnCaptured(object? sender, KeyCapturedEventArgs e)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"KeyData:   {e.KeyData}");
        sb.AppendLine($"Bare VK:   {e.KeyData & Keys.KeyCode}");
        sb.AppendLine($"Modifiers: {e.KeyData & Keys.Modifiers}");
        sb.AppendLine();
        sb.AppendLine(e.Char.HasValue
            ? $"Char:      '{e.Char.Value}'  (U+{(int)e.Char.Value:X4})"
            : "Char:      (none — non-printable key)");
        sb.AppendLine();
        sb.AppendLine($"Ambiguous L/R: {(e.Ambiguous ? "YES" : "no")}");
        if (e.AmbiguityNote != null) sb.AppendLine($"Note: {e.AmbiguityNote}");
        _details.Text = sb.ToString();
    }
}
