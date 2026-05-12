namespace PixivUtil.Windows.Models;

public sealed record PreviewItem(
    string Id,
    string Title,
    string ArtistName,
    string PageUrl,
    string? ThumbnailUrl,
    string? LocalPath = null)
{
    public Uri? ThumbnailUri =>
        string.IsNullOrWhiteSpace(ThumbnailUrl)
            ? null
            : new Uri(ThumbnailUrl, UriKind.Absolute);

    public Uri PageUri => new(PageUrl, UriKind.Absolute);
}
