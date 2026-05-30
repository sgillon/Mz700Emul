using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MZ700Emul;

/// <summary>
/// User preferences persisted across runs as a small INI file
/// (<c>settings.ini</c>) next to the executable. INI was picked over
/// JSON because it's trivially human-readable and editable when the
/// user wants to tweak something by hand.
///
/// To add a new setting: declare a property, read it from the parsed
/// dictionary in <see cref="Load"/>, and write it in <see cref="Save"/>.
/// </summary>
public sealed class Settings
{
    public int DisplayScale { get; set; } = 2;

    // Paths to the system files (monitor ROM, character font, BASIC
    // cassette image). Populated automatically on first run by scanning
    // the program directory; the user can edit settings.ini afterwards
    // to point them somewhere else.
    //
    // The raw *Path strings are written to disk verbatim — kept as a
    // path RELATIVE to the executable when the file lives at-or-below
    // it, otherwise as an absolute path. Use the *FullPath helpers
    // when actually opening the file.
    public string MonitorRomPath { get; set; } = "";
    public string FontPath { get; set; } = "";
    public string BasicPath { get; set; } = "";

    // PC gamepad button index (0..31, matching the WinMM dwButtons
    // bitmask) that drives each MZ-1X03 stick button. Defaults match
    // the original hardcoded behaviour: button 0 → MZ SW1, button 1
    // → MZ SW2. Both emulated sticks share the same mapping; if you
    // need per-slot mappings, this can be split into JoyStick1Button1
    // etc. later without breaking the existing keys.
    public int JoyButton1Index { get; set; } = 0;
    public int JoyButton2Index { get; set; } = 1;

    public string MonitorRomFullPath => Resolve(MonitorRomPath);
    public string FontFullPath => Resolve(FontPath);
    public string BasicFullPath => Resolve(BasicPath);

    private static string FilePath =>
        Path.Combine(AppContext.BaseDirectory, "settings.ini");

    public static Settings Load()
    {
        var s = new Settings();
        bool fileExisted = File.Exists(FilePath);
        bool missingSection = false;
        try
        {
            if (fileExisted)
            {
                var ini = ParseIni(File.ReadAllLines(FilePath));
                s.DisplayScale = GetInt(ini, "Display", "Scale", s.DisplayScale);
                s.MonitorRomPath = GetString(ini, "Roms", "Monitor", "");
                s.FontPath = GetString(ini, "Roms", "Font", "");
                s.BasicPath = GetString(ini, "Roms", "Basic", "");
                s.JoyButton1Index = GetInt(ini, "Joystick", "Button1", s.JoyButton1Index);
                s.JoyButton2Index = GetInt(ini, "Joystick", "Button2", s.JoyButton2Index);
                // Older settings.ini files predate sections added in later
                // versions. Flag any missing section so Save() runs once
                // and the user gets a complete, editable file.
                if (!ini.ContainsKey("Joystick")) missingSection = true;
            }
        }
        catch { /* fall through to defaults */ }
        s.Sanitize();
        // Auto-detect any ROM paths that are empty or point at a missing
        // file. On the very first run this populates all three; on later
        // runs it self-heals if the user moved files around.
        bool dirty = s.EnsureRomPaths();
        if (dirty || !fileExisted || missingSection) s.Save();
        return s;
    }

    public void Save()
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("; MZ700 Emulator settings — edit by hand if you like.");
            sb.AppendLine();
            sb.AppendLine("[Display]");
            sb.AppendLine($"Scale={DisplayScale}");
            sb.AppendLine();
            sb.AppendLine("[Roms]");
            sb.AppendLine($"Monitor={MonitorRomPath}");
            sb.AppendLine($"Font={FontPath}");
            sb.AppendLine($"Basic={BasicPath}");
            sb.AppendLine();
            sb.AppendLine("[Joystick]");
            sb.AppendLine("; PC gamepad button index (0..31) that drives each MZ-1X03 stick button.");
            sb.AppendLine($"Button1={JoyButton1Index}");
            sb.AppendLine($"Button2={JoyButton2Index}");
            File.WriteAllText(FilePath, sb.ToString());
        }
        catch { /* non-fatal */ }
    }

    private void Sanitize()
    {
        if (DisplayScale < 1 || DisplayScale > 3) DisplayScale = 2;
        if (JoyButton1Index < 0 || JoyButton1Index > 31) JoyButton1Index = 0;
        if (JoyButton2Index < 0 || JoyButton2Index > 31) JoyButton2Index = 1;
    }

    /// <summary>
    /// Fills in any ROM/font/BASIC path that's empty or no longer points
    /// at an existing file by scanning the program directory and its
    /// <c>roms/</c> / <c>basic/</c> subdirectories (walking up the tree
    /// so dev-time runs from <c>bin/Debug/...</c> still find files at
    /// the source-tree root). Returns true if anything changed.
    /// </summary>
    private bool EnsureRomPaths()
    {
        bool dirty = false;
        dirty |= EnsureOne(
            MonitorRomFullPath, MonitorRomPath, v => MonitorRomPath = v,
            () => FindFile("1z-013a.rom", "roms", ""));
        dirty |= EnsureOne(
            FontFullPath, FontPath, v => FontPath = v,
            // Prefer the binary font over the hex text dump.
            () => FindFile("mz700fon.int", "roms", "") ?? FindFile("font_hex.txt", "roms", ""));
        dirty |= EnsureOne(
            BasicFullPath, BasicPath, v => BasicPath = v,
            // BASIC is conceptually another ROM image; scan the same
            // places, plus the legacy basic/ folder for back-compat.
            () => FindFile("1Z-013B.mzf", "roms", "basic", ""));
        return dirty;
    }

    /// <summary>
    /// Either auto-detects a missing path or normalizes an existing one
    /// to the relative-when-possible storage form. Returns true if the
    /// stored value changed.
    /// </summary>
    private static bool EnsureOne(string fullPath, string storedPath, Action<string> setStored, Func<string?> scan)
    {
        if (IsExistingFile(fullPath))
        {
            var normalized = MakeStorable(fullPath);
            if (normalized != storedPath) { setStored(normalized); return true; }
            return false;
        }
        var found = scan();
        if (found == null) return false;
        var storable = MakeStorable(found);
        if (storable == storedPath) return false;
        setStored(storable);
        return true;
    }

    private static bool IsExistingFile(string path) =>
        !string.IsNullOrEmpty(path) && File.Exists(path);

    /// <summary>
    /// Converts a raw stored path (relative or absolute) into an
    /// absolute path. Relative paths are resolved against
    /// <see cref="AppContext.BaseDirectory"/>. Empty stays empty.
    /// </summary>
    private static string Resolve(string storedPath)
    {
        if (string.IsNullOrEmpty(storedPath)) return storedPath;
        if (Path.IsPathRooted(storedPath)) return storedPath;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, storedPath));
    }

    /// <summary>
    /// Picks the form to write to settings.ini for an absolute path:
    /// relative-to-base when the file lives under the executable
    /// directory (so the INI file stays portable), absolute otherwise.
    /// </summary>
    private static string MakeStorable(string absolutePath)
    {
        var baseDir = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(absolutePath);
        var prefix = baseDir + Path.DirectorySeparatorChar;
        if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return full.Substring(prefix.Length);
        return full;
    }

    /// <summary>
    /// Walks up the directory tree from <see cref="AppContext.BaseDirectory"/>
    /// looking for <paramref name="filename"/> inside any of the listed
    /// subdirectories at each level. Empty string in <paramref name="subdirs"/>
    /// means "the level itself".
    /// </summary>
    private static string? FindFile(string filename, params string[] subdirs)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            foreach (var sub in subdirs)
            {
                var candidate = string.IsNullOrEmpty(sub)
                    ? Path.Combine(dir, filename)
                    : Path.Combine(dir, sub, filename);
                if (File.Exists(candidate)) return candidate;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }

    // --- minimal INI parser: [Section] headers, key=value lines, ';' or '#' comments. ---
    private static Dictionary<string, Dictionary<string, string>> ParseIni(string[] lines)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        result[""] = current;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
            if (line[0] == '[' && line[^1] == ']')
            {
                var name = line.Substring(1, line.Length - 2).Trim();
                if (!result.TryGetValue(name, out current!))
                {
                    current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    result[name] = current;
                }
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            current[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
        }
        return result;
    }

    private static int GetInt(Dictionary<string, Dictionary<string, string>> ini, string section, string key, int fallback)
    {
        if (ini.TryGetValue(section, out var s) && s.TryGetValue(key, out var v) &&
            int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            return n;
        return fallback;
    }

    private static string GetString(Dictionary<string, Dictionary<string, string>> ini, string section, string key, string fallback)
    {
        if (ini.TryGetValue(section, out var s) && s.TryGetValue(key, out var v))
            return v;
        return fallback;
    }
}
