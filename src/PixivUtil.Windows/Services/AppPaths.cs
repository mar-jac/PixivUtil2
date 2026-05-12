namespace PixivUtil.Windows.Services;

public sealed class AppPaths
{
    public AppPaths()
    {
        AppDirectory = AppContext.BaseDirectory;

        var repoRoot = FindRepositoryRoot(AppDirectory);
        if (repoRoot is not null)
        {
            AppDirectory = repoRoot;
        }
    }

    public string AppDirectory { get; }

    public string ConfigPath => Path.Combine(AppDirectory, "config.ini");

    public string DefaultDatabasePath => Path.Combine(AppDirectory, "db.sqlite");

    public string? LegacyExecutablePath
    {
        get
        {
            var exe = Path.Combine(AppDirectory, "PixivUtil2.exe");
            if (File.Exists(exe))
            {
                return exe;
            }

            var script = Path.Combine(AppDirectory, "PixivUtil2.py");
            return File.Exists(script) ? script : null;
        }
    }

    private static string? FindRepositoryRoot(string start)
    {
        var current = new DirectoryInfo(start);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "PixivUtil2.py")) &&
                Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}
