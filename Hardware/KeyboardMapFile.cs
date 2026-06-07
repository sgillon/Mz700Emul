using System;
using System.IO;

namespace MZ700Emul.Hardware;

/// <summary>
/// Save / load helpers for the <c>.mzkbd</c> keyboard-mapping exchange
/// file. The file is a small INI: two sections (<c>[CharMap]</c> and
/// <c>[KeyOverrides]</c>) using the same line formats
/// <see cref="CharMapOverrides.SerialiseLines"/> and
/// <see cref="KeyOverride.SerialiseLines"/> already write into
/// <c>settings.ini</c>, plus a header comment block documenting the
/// shape for hand-editors.
///
/// Only user overrides are persisted — built-in defaults are applied at
/// runtime and don't need to round-trip through the file. Import
/// offers <i>merge</i> (apply on top of current overrides) and
/// <i>replace</i> (clear current first); the caller drives that prompt.
/// </summary>
public static class KeyboardMapFile
{
    public const string FileExtension = ".mzkbd";
    public const string FileFilter =
        "MZ-700 keyboard mapping (*.mzkbd)|*.mzkbd|All files|*.*";

    /// <summary>
    /// Writes the contents of <paramref name="charOverrides"/> and
    /// <paramref name="keyOverrides"/> to <paramref name="path"/>. The
    /// built-in defaults are not included.
    /// </summary>
    public static void Save(string path, CharMapOverrides charOverrides, KeyOverride keyOverrides)
    {
        using var w = new StreamWriter(path);
        w.WriteLine("; MZ-700 Keyboard mapping file");
        w.WriteLine($"; Saved {DateTime.Now:yyyy-MM-dd HH:mm:ss} by MZ700Emul.");
        w.WriteLine(";");
        w.WriteLine("; Contains user-customised PC-keyboard bindings only — the");
        w.WriteLine("; emulator's built-in defaults are still applied and don't");
        w.WriteLine("; need to be listed here. Load via Settings -> Keyboard ->");
        w.WriteLine("; Import...");
        w.WriteLine();
        w.WriteLine("[CharMap]");
        w.WriteLine("; PC character (4-digit hex Unicode codepoint) = Row,Col,Shift");
        w.WriteLine("; Shift: t = MZ shift forced ON, f = forced OFF");
        foreach (var line in charOverrides.SerialiseLines())
            w.WriteLine(line);
        w.WriteLine();
        w.WriteLine("[KeyOverrides]");
        w.WriteLine("; PC virtual key (with modifiers) = Row,Col,Shift");
        w.WriteLine("; Shift: t = forced ON, f = forced OFF, - = pass-through PC shift");
        foreach (var line in keyOverrides.SerialiseLines())
            w.WriteLine(line);
    }

    /// <summary>
    /// Parses <paramref name="path"/> into fresh override layers — the
    /// caller decides whether to merge them into the live settings or
    /// replace what's there. Returns the count of entries actually
    /// loaded into each layer so the caller can confirm with the user.
    /// </summary>
    public static (CharMapOverrides chars, KeyOverride vks) Load(string path)
    {
        var chars = new CharMapOverrides();
        var vks = new KeyOverride();

        string? section = null;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw;
            int comment = line.IndexOf(';');
            if (comment >= 0) line = line.Substring(0, comment);
            line = line.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                section = line.Substring(1, line.Length - 2).Trim();
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line.Substring(0, eq).Trim();
            string val = line.Substring(eq + 1).Trim();
            switch (section)
            {
                case "CharMap":      chars.TryParseLine(key, val); break;
                case "KeyOverrides": vks.TryParseLine(key, val);   break;
            }
        }
        return (chars, vks);
    }
}
