using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using PixivUtil.Windows.Models;

namespace PixivUtil.Windows.Services;

public sealed class PixivApiClient : IDisposable
{
    private readonly PixivSettingsStore _settingsStore;
    private readonly CookieContainer _cookies = new();
    private readonly HttpClientHandler? _handler;
    private readonly HttpClient _httpClient;

    public PixivApiClient()
        : this(new PixivSettingsStore(new AppPaths()))
    {
    }

    public PixivApiClient(PixivSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        _handler = new HttpClientHandler
        {
            CookieContainer = _cookies,
            AutomaticDecompression = DecompressionMethods.All
        };

        _httpClient = new HttpClient(_handler);
        ConfigureDefaultHeaders(_httpClient);
    }

    internal PixivApiClient(PixivSettingsStore settingsStore, HttpMessageHandler handler)
    {
        _settingsStore = settingsStore;
        _handler = null;
        _httpClient = new HttpClient(handler);
        ConfigureDefaultHeaders(_httpClient);
    }

    public async Task<IReadOnlyList<PreviewItem>> GetNewFollowedPreviewsAsync(
        int page = 1,
        string mode = "all",
        CancellationToken cancellationToken = default)
    {
        ApplyCookies();

        var uri = new Uri($"https://www.pixiv.net/ajax/follow_latest/illust?p={page}&mode={Uri.EscapeDataString(mode)}&lang=en");
        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        ThrowIfPixivError(document.RootElement);

        var body = document.RootElement.GetProperty("body");
        var previews = ParseThumbnailItems(body).ToList();
        if (previews.Count > 0)
        {
            return previews;
        }

        if (body.TryGetProperty("page", out var pageNode) &&
            pageNode.TryGetProperty("ids", out var idsNode) &&
            idsNode.ValueKind == JsonValueKind.Array)
        {
            foreach (var id in idsNode.EnumerateArray().Select(static node => node.GetString()).Where(static id => !string.IsNullOrWhiteSpace(id)))
            {
                previews.Add(await GetArtworkPreviewAsync(id!, cancellationToken));
            }
        }

        return previews;
    }

    public async Task<PreviewItem> GetArtworkPreviewAsync(
        string artworkId,
        CancellationToken cancellationToken = default)
    {
        ApplyCookies();

        var uri = new Uri($"https://www.pixiv.net/ajax/illust/{Uri.EscapeDataString(artworkId)}?lang=en");
        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        ThrowIfPixivError(document.RootElement);

        var body = document.RootElement.GetProperty("body");
        var title = GetString(body, "title") ?? $"Artwork {artworkId}";
        var artist = GetString(body, "userName") ?? GetString(body, "userAccount") ?? "Unknown artist";
        var thumbnail = TryGetUrl(body, "thumb") ?? TryGetUrl(body, "small") ?? TryGetUrl(body, "regular");

        return new PreviewItem(
            artworkId,
            title,
            artist,
            $"https://www.pixiv.net/artworks/{artworkId}",
            thumbnail,
            ContentType: GetString(body, "illustType") ?? GetString(body, "xRestrict") ?? "Illust",
            PageCount: GetInt(body, "pageCount") ?? 1,
            BookmarkCount: GetInt(body, "bookmarkCount") ?? 0,
            IsR18: GetInt(body, "xRestrict") > 0);
    }

    private void ApplyCookies()
    {
        var settings = _settingsStore.Load();
        if (!string.IsNullOrWhiteSpace(settings.PixivCookie))
        {
            _cookies.Add(new Cookie("PHPSESSID", settings.PixivCookie, "/", ".pixiv.net"));
        }
    }

    private static IEnumerable<PreviewItem> ParseThumbnailItems(JsonElement body)
    {
        if (!body.TryGetProperty("thumbnails", out var thumbnails) ||
            !thumbnails.TryGetProperty("illust", out var illusts) ||
            illusts.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in illusts.EnumerateArray())
        {
            var id = GetString(item, "id") ?? GetString(item, "illustId");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            yield return new PreviewItem(
                id,
                GetString(item, "title") ?? $"Artwork {id}",
                GetString(item, "userName") ?? "Unknown artist",
                $"https://www.pixiv.net/artworks/{id}",
                TryGetUrl(item, "url") ?? TryGetUrl(item, "thumb") ?? TryGetUrl(item, "regular"),
                ContentType: GetString(item, "illustType") ?? "Illust",
                PageCount: GetInt(item, "pageCount") ?? 1,
                BookmarkCount: GetInt(item, "bookmarkCount") ?? 0,
                IsR18: GetInt(item, "xRestrict") > 0);
        }
    }

    private static string? TryGetUrl(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty("urls", out var urls) && urls.TryGetProperty(propertyName, out var url))
        {
            return url.GetString();
        }

        return element.TryGetProperty(propertyName, out var directUrl)
            ? directUrl.GetString()
            : null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(property.GetString(), out var value) => value,
            _ => null
        };
    }

    private static void ThrowIfPixivError(JsonElement root)
    {
        if (root.TryGetProperty("error", out var error) &&
            error.ValueKind is JsonValueKind.True)
        {
            var message = root.TryGetProperty("message", out var messageNode)
                ? messageNode.GetString()
                : "Pixiv returned an error response.";
            throw new InvalidOperationException(message);
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler?.Dispose();
    }

    private static void ConfigureDefaultHeaders(HttpClient httpClient)
    {
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/135.0 Safari/537.36 PixivUtilWindows/1.0");
        httpClient.DefaultRequestHeaders.Referrer = new Uri("https://www.pixiv.net/");
        httpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
    }
}
