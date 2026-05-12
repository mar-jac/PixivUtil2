namespace PixivUtil.Windows.Models;

public sealed record DownloadRequest(
    DownloadMode Mode,
    string Input,
    int StartPage,
    int EndPage,
    bool IncludeSketch,
    bool UseWildcardTags,
    int BookmarkCountLimit);
