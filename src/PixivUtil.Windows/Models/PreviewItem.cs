namespace PixivUtil.Windows.Models;

public sealed record PreviewItem(
    string Id,
    string Title,
    string ArtistName,
    string PageUrl,
    string? ThumbnailUrl,
    string? LocalPath = null,
    string ContentType = "Illust",
    int PageCount = 1,
    int BookmarkCount = 0,
    bool IsR18 = false)
{
    public Uri? ThumbnailUri =>
        string.IsNullOrWhiteSpace(ThumbnailUrl)
            ? null
            : new Uri(ThumbnailUrl, UriKind.Absolute);

    public Uri PageUri => new(PageUrl, UriKind.Absolute);

    public string Summary =>
        $"{ContentType} | {Math.Max(PageCount, 1)} page(s)" +
        (BookmarkCount > 0 ? $" | {BookmarkCount:N0} bookmarks" : "") +
        (IsR18 ? " | R-18" : "");
}
