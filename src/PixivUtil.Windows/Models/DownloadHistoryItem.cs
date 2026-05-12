namespace PixivUtil.Windows.Models;

public sealed record DownloadHistoryItem(
    string Source,
    string Id,
    string Title,
    string? MemberId,
    string? LocalPath,
    DateTimeOffset? LastUpdated)
{
    public bool HasLocalFile => !string.IsNullOrWhiteSpace(LocalPath) && File.Exists(LocalPath);

    public Uri? LocalPreviewUri =>
        HasLocalFile && IsPreviewableImage(LocalPath!)
            ? new Uri(LocalPath!)
            : null;

    public string LastUpdatedDisplay => LastUpdated?.ToString("g") ?? "";

    private static bool IsPreviewableImage(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp";
    }
}
