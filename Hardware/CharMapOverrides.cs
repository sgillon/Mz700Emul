using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MZ700Emul.Hardware;

/// <summary>
/// User-editable override layer for the character-driven keyboard map.
/// Consulted ahead of <see cref="CharMap.Defaults"/> in
/// <see cref="CharMap.TryLookup"/>, so a user can rebind any PC character
/// (the Unicode char a host keystroke produces) to a different MZ-700
/// matrix slot without touching the built-in defaults.
///
/// Persisted to the <c>[CharMap]</c> section of <c>settings.ini</c> via
/// <see cref="SerialiseLines"/> / <see cref="TryParseLine"/>. Mirrors
/// <see cref="KeyOverride"/>'s shape, keyed by char rather than VK.
///
/// Shift state is a definite <c>bool</c> here (no pass-through tri-state
/// like <see cref="KeyOverride"/>) because by the time a host keystroke
/// has produced a Unicode char, the OS has already resolved the modifier.
/// </summary>
public sealed class CharMapOverrides
{
    private readonly Dictionary<char, CharMap.Press> _map = new();

    public bool TryLookup(char c, out CharMap.Press press) => _map.TryGetValue(c, out press);

    public void Set(char c, CharMap.Press press) => _map[c] = press;
    public void Remove(char c) => _map.Remove(c);
    public void Clear() => _map.Clear();
    public int Count => _map.Count;

    public IEnumerable<KeyValuePair<char, CharMap.Press>> All => _map;

    // ---- INI serialisation -------------------------------------------------

    /// <summary>
    /// Serialise each binding as <c>HHHH=Row,Col,Shift   ; '&lt;glyph&gt;'</c>
    /// where HHHH is the 4-digit hex Unicode codepoint of the PC char (hex
    /// avoids breaking the INI parser on chars like <c>=</c>, <c>;</c>,
    /// <c>#</c>) and Shift is <c>t</c> (assert MZ shift) or <c>f</c>
    /// (clear it). The trailing comment shows the literal glyph when
    /// printable ASCII, purely for hand-editing readability.
    /// </summary>
    public IEnumerable<string> SerialiseLines() =>
        _map.OrderBy(kv => (int)kv.Key)
            .Select(kv => $"{(int)kv.Key:X4}={kv.Value.Row},{kv.Value.Col},{ShiftChar(kv.Value.MzShift)}{GlyphComment(kv.Key)}");

    /// <summary>
    /// Parses one INI line (key=value, the comment already stripped by the
    /// caller). Returns true on success and updates the map; false if the
    /// line can't be decoded (INI is forgiving — silent skip).
    /// </summary>
    public bool TryParseLine(string keyName, string value)
    {
        if (!int.TryParse(keyName, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codepoint)) return false;
        if (codepoint < 0 || codepoint > 0xFFFF) return false;
        var parts = value.Split(',');
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var row)) return false;
        if (!int.TryParse(parts[1], out var col)) return false;
        if (row < 0 || row > 9 || col < 0 || col > 7) return false;
        bool shift = parts[2].Trim() switch
        {
            "t" or "T" => true,
            _ => false,
        };
        _map[(char)codepoint] = new CharMap.Press(row, col, shift);
        return true;
    }

    private static string ShiftChar(bool s) => s ? "t" : "f";

    private static string GlyphComment(char c) =>
        c >= 0x20 && c <= 0x7E ? $"   ; '{c}'" : "";
}
