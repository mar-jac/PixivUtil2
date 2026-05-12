namespace PixivUtil.Windows.Models;

public sealed record DownloadMode(
    string Key,
    string Name,
    string Description,
    bool RequiresInput = true,
    string InputHint = "IDs, tags, or file path")
{
    public static IReadOnlyList<DownloadMode> All { get; } =
    [
        new("1", "Member artworks", "Download by member ID", InputHint: "Member IDs"),
        new("2", "Artwork IDs", "Download by image/artwork ID", InputHint: "Artwork IDs"),
        new("3", "Tag search", "Download by tags", InputHint: "Tags"),
        new("4", "List file", "Download from list.txt-compatible files", InputHint: "List file path"),
        new("5", "Followed artists", "Download from followed Pixiv artists", RequiresInput: false),
        new("6", "Bookmarked images", "Download bookmarked images", RequiresInput: false, InputHint: "Optional tag"),
        new("7", "Tags list", "Download from tags.txt-compatible files", InputHint: "Tags file path"),
        new("8", "New from followed", "Download new content from followed artists", RequiresInput: false),
        new("9", "Title/caption", "Download by title or caption search", InputHint: "Search text"),
        new("10", "Tag and member", "Download by tag and member ID", InputHint: "Member ID then tags"),
        new("11", "Member bookmarks", "Download another user's bookmarks", InputHint: "Member ID"),
        new("12", "Group", "Download by group ID", InputHint: "Group ID"),
        new("13", "Manga series", "Download by manga series ID", InputHint: "Manga series IDs"),
        new("14", "Novel", "Download by novel ID", InputHint: "Novel IDs"),
        new("15", "Novel series", "Download by novel series ID", InputHint: "Novel series IDs"),
        new("16", "Ranking", "Download ranking content", RequiresInput: false),
        new("17", "R-18 ranking", "Download R-18 ranking content", RequiresInput: false),
        new("18", "Pixiv new illusts", "Download latest Pixiv illust/manga feed", RequiresInput: false),
        new("19", "Unlisted artwork", "Download by unlisted image ID", InputHint: "Unlisted artwork IDs"),
        new("f1", "FANBOX supported", "Download from supported FANBOX artists", RequiresInput: false),
        new("f2", "FANBOX creator", "Download by FANBOX creator ID", InputHint: "Creator IDs"),
        new("f3", "FANBOX post", "Download by FANBOX post ID", InputHint: "Post IDs"),
        new("f4", "FANBOX followed", "Download from followed FANBOX artists", RequiresInput: false),
        new("f5", "FANBOX custom list", "Download from listfanbox.txt", RequiresInput: false),
        new("s1", "Sketch artist", "Download Pixiv Sketch by artist ID", InputHint: "Artist ID"),
        new("s2", "Sketch post", "Download Pixiv Sketch by post ID", InputHint: "Post ID"),
        new("b", "Batch job", "Run batch_job.json", InputHint: "Optional batch file path")
    ];
}
