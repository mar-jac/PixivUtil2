namespace PixivUtil.Windows.Models;

public sealed record AppSettings(
    string AppDirectory,
    string ConfigPath,
    string DatabasePath,
    string RootDirectory,
    string PixivCookie,
    string FanboxCookie);
