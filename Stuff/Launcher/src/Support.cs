using System.Xml.Linq;

namespace GameLauncher;

internal static class Log
{
    private static readonly object Gate = new();
    private static readonly string FilePath = Path.Combine(Program.LauncherDir, "launcher.log");

    public static void Init()
    {
        try { if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 512 * 1024) File.Delete(FilePath); }
        catch { }
    }

    public static void Info(string message)
    {
        lock (Gate)
        {
            try { File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}"); }
            catch { }
        }
    }

    public static void Error(string message, Exception ex) => Info($"ERROR {message}: {ex}");
}

internal static class Fmt
{
    public static string Bytes(long b)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = b; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{v:0} {u[i]}" : $"{v:0.0} {u[i]}";
    }
}

/// <summary>Minimal INI reader/writer that preserves comments and layout when saving.</summary>
internal sealed class IniFile
{
    private readonly string _path;
    private readonly List<string> _lines;
    private bool _dirty;

    public IniFile(string path)
    {
        _path = path;
        List<string> lines;
        try { lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>(); }
        catch (IOException) { lines = new List<string>(); }
        _lines = lines;
    }

    public string? Get(string section, string key)
    {
        var (line, _) = Locate(section, key);
        if (line < 0) return null;
        string t = _lines[line];
        return t.Substring(t.IndexOf('=') + 1).Trim();
    }

    public void Set(string section, string key, string value)
    {
        var (line, end) = Locate(section, key);
        string newLine = $"{key}={value}";
        if (line >= 0)
        {
            if (Get(section, key) == value) return;
            _lines[line] = newLine;
        }
        else if (end >= 0) _lines.Insert(end + 1, newLine);
        else
        {
            if (_lines.Count > 0 && _lines[_lines.Count - 1].Trim().Length != 0) _lines.Add("");
            _lines.Add($"[{section}]");
            _lines.Add(newLine);
        }
        _dirty = true;
    }

    public void Save()
    {
        if (!_dirty) return;
        try { File.WriteAllLines(_path, _lines); _dirty = false; }
        catch (Exception ex) { Log.Error("Could not save settings.ini", ex); }
    }

    // Returns the line index of the key (or -1) and the last line index belonging to the section (or -1).
    private (int keyLine, int sectionEnd) Locate(string section, string key)
    {
        string current = "";
        int keyLine = -1, sectionEnd = -1;
        for (int i = 0; i < _lines.Count; i++)
        {
            string t = _lines[i].Trim();
            if (t.Length > 1 && t[0] == '[' && t[t.Length - 1] == ']')
            {
                current = t.Substring(1, t.Length - 2).Trim();
                if (current.Equals(section, StringComparison.OrdinalIgnoreCase)) sectionEnd = i;
                continue;
            }
            if (!current.Equals(section, StringComparison.OrdinalIgnoreCase)) continue;
            if (t.Length == 0 || t[0] is ';' or '#') continue;

            sectionEnd = i;
            int eq = t.IndexOf('=');
            if (eq > 0 && keyLine < 0 && t.Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                keyLine = i;
        }
        return (keyLine, sectionEnd);
    }
}

internal sealed class Manifest
{
    public Version? LauncherVersion { get; init; }
    public Uri? LauncherUrl { get; init; }
    public string? LauncherSha256 { get; init; }

    public Version? AppVersion { get; init; }
    public Uri? AppUrl { get; init; }
    public string? AppSha256 { get; init; }
    public string? AppExecutable { get; init; }
    public string? AppArguments { get; init; }

    public Uri? LogoUrl { get; init; }
    public IReadOnlyList<string> News { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Preserve { get; init; } = Array.Empty<string>();

    public static Version? ParseVersion(string? text)
    {
        if (!Version.TryParse(text, out var v)) return null;
        // Normalise so "1.2.0" and "1.2.0.0" compare equal.
        return new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
    }

    public static Manifest Parse(string xml, Uri baseUri)
    {
        var root = XDocument.Parse(xml).Root ?? throw new InvalidDataException("Empty manifest.");

        string? S(string name)
        {
            var v = root.Element(name)?.Value.Trim();
            return string.IsNullOrEmpty(v) ? null : v;
        }
        Uri? U(string name) => S(name) is { } s ? new Uri(baseUri, s) : null;

        var newsEl = root.Element("news");
        var news = new List<string>();
        if (newsEl != null)
        {
            var items = newsEl.Elements("item").Select(e => e.Value.Trim()).Where(s => s.Length > 0).ToList();
            if (items.Count > 0) news = items;
            else if (newsEl.Value.Trim().Length > 0) news.Add(newsEl.Value.Trim());
        }

        var preserve = root.Element("preserve")?.Elements("path")
        .Select(e => e.Value.Trim().Replace('\\', '/').Trim('/'))
        .Where(s => s.Length > 0).ToList() ?? new List<string>();

        return new Manifest
        {
            LauncherVersion = ParseVersion(S("launcherVersion")),
            LauncherUrl = U("launcherUrl"),
            LauncherSha256 = S("launcherSha256"),
            AppVersion = ParseVersion(S("appVersion")),
            AppUrl = U("appUrl"),
            AppSha256 = S("appSha256"),
            AppExecutable = S("appExecutable"),
            AppArguments = root.Element("appArguments")?.Value.Trim(),   // empty string is meaningful
            LogoUrl = U("logoUrl"),
            News = news,
            Preserve = preserve
        };
    }
}
