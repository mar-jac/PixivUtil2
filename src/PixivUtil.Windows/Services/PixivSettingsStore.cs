using PixivUtil.Windows.Models;

namespace PixivUtil.Windows.Services;

public sealed class PixivSettingsStore(AppPaths paths)
{
    public AppSettings Load()
    {
        var ini = File.Exists(paths.ConfigPath)
            ? ReadIni(paths.ConfigPath)
            : new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        var rootDirectory = GetValue(ini, "Settings", "rootDirectory", paths.AppDirectory);
        var dbPath = GetValue(ini, "Settings", "dbPath", "");
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            dbPath = paths.DefaultDatabasePath;
        }
        else if (!Path.IsPathRooted(dbPath))
        {
            dbPath = Path.GetFullPath(Path.Combine(paths.AppDirectory, dbPath));
        }

        return new AppSettings(
            paths.AppDirectory,
            paths.ConfigPath,
            dbPath,
            rootDirectory,
            GetValue(ini, "Authentication", "cookie", ""),
            GetValue(ini, "Authentication", "cookieFanbox", ""));
    }

    public void SavePixivCookie(string cookie)
    {
        var ini = File.Exists(paths.ConfigPath)
            ? ReadIni(paths.ConfigPath)
            : new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        if (!ini.TryGetValue("Authentication", out var authentication))
        {
            authentication = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ini["Authentication"] = authentication;
        }

        authentication["cookie"] = cookie;
        WriteIni(paths.ConfigPath, ini);
    }

    private static string GetValue(
        IReadOnlyDictionary<string, Dictionary<string, string>> ini,
        string section,
        string key,
        string defaultValue)
    {
        return ini.TryGetValue(section, out var sectionValues) &&
               sectionValues.TryGetValue(key, out var value)
            ? value
            : defaultValue;
    }

    private static Dictionary<string, Dictionary<string, string>> ReadIni(string path)
    {
        var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? currentSection = null;

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var section = line[1..^1].Trim();
                currentSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                data[section] = currentSection;
                continue;
            }

            if (currentSection is null)
            {
                continue;
            }

            var equals = line.IndexOf('=');
            if (equals < 0)
            {
                continue;
            }

            currentSection[line[..equals].Trim()] = line[(equals + 1)..].Trim();
        }

        return data;
    }

    private static void WriteIni(
        string path,
        IReadOnlyDictionary<string, Dictionary<string, string>> data)
    {
        using var writer = new StreamWriter(path, append: false);
        foreach (var (section, values) in data)
        {
            writer.WriteLine($"[{section}]");
            foreach (var (key, value) in values)
            {
                writer.WriteLine($"{key} = {value}");
            }

            writer.WriteLine();
        }
    }
}
