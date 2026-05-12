using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixivUtil.Windows.Models;
using PixivUtil.Windows.Services;
using Windows.System;

namespace PixivUtil.Windows.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly PixivSettingsStore _settingsStore;
    private readonly PixivDatabase _database;
    private readonly PixivApiClient _apiClient;
    private readonly LegacyPixivBridge _downloadBridge;
    private readonly AppSettings _settings;
    private IReadOnlyList<PreviewItem> _previewCache = [];

    [ObservableProperty]
    private string _pixivCookie = "";

    [ObservableProperty]
    private string _input = "";

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private string _galleryFeed = "Followed";

    [ObservableProperty]
    private string _contentTypeFilter = "All";

    [ObservableProperty]
    private bool _showR18;

    [ObservableProperty]
    private bool _autoDownloadNew;

    [ObservableProperty]
    private double _startPage = 1;

    [ObservableProperty]
    private double _endPage;

    [ObservableProperty]
    private double _bookmarkCountLimit = -1;

    [ObservableProperty]
    private bool _includeSketch;

    [ObservableProperty]
    private bool _useWildcardTags;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private string _commandOutput = "";

    [ObservableProperty]
    private DownloadMode? _selectedDownloadMode;

    [ObservableProperty]
    private PreviewItem? _selectedPreview;

    [ObservableProperty]
    private FollowedArtist? _selectedArtist;

    [ObservableProperty]
    private DownloadHistoryItem? _selectedHistoryItem;

    public MainViewModel()
        : this(
            new PixivSettingsStore(new AppPaths()),
            new PixivDatabase(new PixivSettingsStore(new AppPaths())),
            new PixivApiClient(new PixivSettingsStore(new AppPaths())),
            new LegacyPixivBridge(new AppPaths()))
    {
    }

    public MainViewModel(
        PixivSettingsStore settingsStore,
        PixivDatabase database,
        PixivApiClient apiClient,
        LegacyPixivBridge downloadBridge)
    {
        _settingsStore = settingsStore;
        _database = database;
        _apiClient = apiClient;
        _downloadBridge = downloadBridge;
        _settings = _settingsStore.Load();

        DownloadModes = new ObservableCollection<DownloadMode>(DownloadMode.All);
        SelectedDownloadMode = DownloadModes.FirstOrDefault(static mode => mode.Key == "8") ?? DownloadModes.FirstOrDefault();

        GalleryFeeds = new ObservableCollection<string>(["Preview", "New", "Followed", "Trending", "Popular"]);
        ContentTypeFilters = new ObservableCollection<string>(["All", "Illust", "Manga", "Ugoira"]);
        PixivCookie = _settings.PixivCookie;
        Status = _downloadBridge.IsAvailable
            ? "Ready"
            : "Ready for previews/history. Download bridge not found yet.";
    }

    public ObservableCollection<DownloadMode> DownloadModes { get; }

    public ObservableCollection<string> GalleryFeeds { get; }

    public ObservableCollection<string> ContentTypeFilters { get; }

    public ObservableCollection<PreviewItem> NewFollowedPreviews { get; } = [];

    public ObservableCollection<FollowedArtist> FollowedArtists { get; } = [];

    public ObservableCollection<DownloadHistoryItem> AccountHistory { get; } = [];

    public ObservableCollection<DownloadHistoryItem> VisibleHistory { get; } = [];

    public ObservableCollection<DownloadJob> DownloadJobs { get; } = [];

    public string AccountStatus => string.IsNullOrWhiteSpace(PixivCookie)
        ? "Pixiv cookie not configured"
        : "Pixiv cookie connected";

    public string BackendStatus => _downloadBridge.IsAvailable
        ? "Downloader bridge ready"
        : "Downloader bridge missing";

    public string DatabasePath => _settings.DatabasePath;

    public string DownloadRoot => _settings.RootDirectory;

    public string GalleryTitle => GalleryFeed switch
    {
        "New" => "New Pixiv artwork",
        "Trending" => "Trending now",
        "Popular" => "Most popular",
        "Preview" => "Preview gallery",
        _ => "Updated artwork from followed artists"
    };

    public string GalleryStatusLine => $"{NewFollowedPreviews.Count:N0} works in {GalleryFeed.ToLowerInvariant()} view";

    [RelayCommand]
    private void SaveCookie()
    {
        _settingsStore.SavePixivCookie(PixivCookie.Trim());
        Status = "Pixiv cookie saved to config.ini.";
    }

    partial void OnPixivCookieChanged(string value) => OnPropertyChanged(nameof(AccountStatus));

    [RelayCommand]
    private async Task RefreshHistoryAsync()
    {
        await RunBusyAsync("Loading account history...", async cancellationToken =>
        {
            await LoadHistoryCoreAsync(cancellationToken);
        });
    }

    [RelayCommand]
    private async Task RefreshWorkspaceAsync()
    {
        await RunBusyAsync("Refreshing previews and history...", async cancellationToken =>
        {
            await LoadFollowedPreviewsCoreAsync(cancellationToken);
            await LoadHistoryCoreAsync(cancellationToken);

            if (AutoDownloadNew && NewFollowedPreviews.Count > 0)
            {
                await RunDownloadJobAsync(
                    "Auto download new followed content",
                    "Triggered after preview refresh.",
                    token => _downloadBridge.DownloadNewFollowedContentAsync(
                        StartPageNumber,
                        EndPageNumber,
                        BookmarkCountLimitNumber,
                        token),
                    cancellationToken);
                await LoadHistoryCoreAsync(cancellationToken);
            }
        });
    }

    [RelayCommand]
    private async Task PreviewNewFollowedAsync()
    {
        await RunBusyAsync("Loading followed-artist previews...", async cancellationToken =>
        {
            await LoadFollowedPreviewsCoreAsync(cancellationToken);
        });
    }

    [RelayCommand]
    private async Task LoadGalleryFeedAsync(string? feed)
    {
        if (!string.IsNullOrWhiteSpace(feed))
        {
            GalleryFeed = feed;
        }

        await RunBusyAsync($"Loading {GalleryFeed} gallery...", async cancellationToken =>
        {
            await LoadGalleryCoreAsync(cancellationToken);
        });
    }

    [RelayCommand]
    private async Task LoadTrendingAsync()
    {
        GalleryFeed = "Trending";
        await RunBusyAsync("Loading trending artwork...", async cancellationToken =>
        {
            await LoadGalleryCoreAsync(cancellationToken);
        });
    }

    [RelayCommand]
    private async Task LoadPopularAsync()
    {
        GalleryFeed = "Popular";
        await RunBusyAsync("Loading popular artwork...", async cancellationToken =>
        {
            await LoadGalleryCoreAsync(cancellationToken);
        });
    }

    [RelayCommand]
    private async Task DownloadNewFollowedAsync()
    {
        await RunBusyAsync("Downloading new followed-artist content...", async cancellationToken =>
        {
            await RunDownloadJobAsync(
                "New followed-artist download",
                $"Pages {StartPageNumber}-{(EndPageNumber == 0 ? "all" : EndPageNumber)}",
                token => _downloadBridge.DownloadNewFollowedContentAsync(
                    StartPageNumber,
                    EndPageNumber,
                    BookmarkCountLimitNumber,
                    token),
                cancellationToken);
            await LoadHistoryCoreAsync(cancellationToken);
        });
    }

    [RelayCommand]
    private async Task DownloadSelectedPreviewAsync()
    {
        if (SelectedPreview is null)
        {
            Status = "Select a preview first.";
            return;
        }

        await RunBusyAsync($"Downloading {SelectedPreview.Title}...", async cancellationToken =>
        {
            var mode = DownloadMode.All.First(static item => item.Key == "2");
            var request = new DownloadRequest(
                mode,
                SelectedPreview.Id,
                StartPage: 1,
                EndPage: 0,
                IncludeSketch: false,
                UseWildcardTags: false,
                BookmarkCountLimit: -1);

            await RunDownloadJobAsync(
                $"Download artwork {SelectedPreview.Id}",
                SelectedPreview.Title,
                token => _downloadBridge.DownloadAsync(request, token),
                cancellationToken);
            await LoadHistoryCoreAsync(cancellationToken);
        });
    }

    [RelayCommand]
    private async Task DownloadSelectedArtistAsync()
    {
        var artistId = SelectedArtist?.MemberId ?? SelectedPreview?.ArtistId;
        var artistName = SelectedArtist?.Name ?? SelectedPreview?.ArtistName ?? "selected artist";
        if (string.IsNullOrWhiteSpace(artistId))
        {
            Status = "Select an artist with a Pixiv user ID first.";
            return;
        }

        await RunBusyAsync($"Downloading works from {artistName}...", async cancellationToken =>
        {
            var mode = DownloadMode.All.First(static item => item.Key == "1");
            var request = new DownloadRequest(
                mode,
                artistId,
                StartPageNumber,
                EndPageNumber,
                IncludeSketch,
                UseWildcardTags: false,
                BookmarkCountLimit: -1);

            await RunDownloadJobAsync(
                $"Download artist {artistId}",
                artistName,
                token => _downloadBridge.DownloadAsync(request, token),
                cancellationToken);
            await LoadHistoryCoreAsync(cancellationToken);
        });
    }


    [RelayCommand]
    private async Task DownloadSelectedModeAsync()
    {
        if (SelectedDownloadMode is null)
        {
            Status = "Select a download mode first.";
            return;
        }

        if (SelectedDownloadMode.RequiresInput && string.IsNullOrWhiteSpace(Input))
        {
            Status = $"Input required: {SelectedDownloadMode.InputHint}.";
            return;
        }

        await RunBusyAsync($"Running {SelectedDownloadMode.Name}...", async cancellationToken =>
        {
            var request = new DownloadRequest(
                SelectedDownloadMode,
                Input,
                StartPageNumber,
                EndPageNumber,
                IncludeSketch,
                UseWildcardTags,
                BookmarkCountLimitNumber);

            await RunDownloadJobAsync(
                SelectedDownloadMode.Name,
                request.Input,
                token => _downloadBridge.DownloadAsync(request, token),
                cancellationToken);
            await LoadHistoryCoreAsync(cancellationToken);
        });
    }

    [RelayCommand]
    private async Task RedownloadSelectedHistoryAsync()
    {
        if (SelectedHistoryItem is null)
        {
            Status = "Select a history item first.";
            return;
        }

        var modeKey = SelectedHistoryItem.Source switch
        {
            "Pixiv" => "2",
            "FANBOX" => "f3",
            "Sketch" => "s2",
            _ => ""
        };

        var mode = DownloadMode.All.FirstOrDefault(item => item.Key == modeKey);
        if (mode is null)
        {
            Status = $"Redownload is not available for {SelectedHistoryItem.Source}.";
            return;
        }

        await RunBusyAsync($"Redownloading {SelectedHistoryItem.Title}...", async cancellationToken =>
        {
            var request = new DownloadRequest(
                mode,
                SelectedHistoryItem.Id,
                StartPage: 1,
                EndPage: 0,
                IncludeSketch: false,
                UseWildcardTags: false,
                BookmarkCountLimit: -1);

            await RunDownloadJobAsync(
                $"Redownload {SelectedHistoryItem.Source} {SelectedHistoryItem.Id}",
                SelectedHistoryItem.Title,
                token => _downloadBridge.DownloadAsync(request, token),
                cancellationToken);
            await LoadHistoryCoreAsync(cancellationToken);
        });
    }

    [RelayCommand]
    private async Task OpenSelectedPreviewAsync()
    {
        if (SelectedPreview is not null)
        {
            await Launcher.LaunchUriAsync(SelectedPreview.PageUri);
        }
    }

    [RelayCommand]
    private async Task OpenSelectedArtistAsync()
    {
        if (SelectedPreview?.ArtistUri is not null)
        {
            await Launcher.LaunchUriAsync(SelectedPreview.ArtistUri);
            return;
        }

        if (SelectedArtist?.MemberId is not null)
        {
            await Launcher.LaunchUriAsync(new Uri($"https://www.pixiv.net/users/{SelectedArtist.MemberId}"));
        }
    }

    [RelayCommand]
    private async Task OpenSelectedHistoryAsync()
    {
        if (SelectedHistoryItem?.HasLocalFile == true)
        {
            await Launcher.LaunchUriAsync(new Uri(SelectedHistoryItem.LocalPath!));
        }
        else if (SelectedHistoryItem is { Source: "Pixiv" } pixiv)
        {
            await Launcher.LaunchUriAsync(new Uri($"https://www.pixiv.net/artworks/{pixiv.Id}"));
        }
    }

    [RelayCommand]
    private Task OpenDownloadRootAsync()
    {
        OpenPath(DownloadRoot);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task OpenDatabaseFolderAsync()
    {
        OpenPath(Path.GetDirectoryName(DatabasePath) ?? _settings.AppDirectory);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ExportHistoryCsvAsync()
    {
        var exportPath = Path.Combine(_settings.AppDirectory, $"pixivutil-history-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.csv");
        await using (var stream = File.Create(exportPath))
        await using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
        {
            await writer.WriteLineAsync("source,id,title,member_id,local_path,last_updated");
            foreach (var item in VisibleHistory)
            {
                await writer.WriteLineAsync(string.Join(
                    ",",
                    Csv(item.Source),
                    Csv(item.Id),
                    Csv(item.Title),
                    Csv(item.MemberId ?? ""),
                    Csv(item.LocalPath ?? ""),
                    Csv(item.LastUpdatedDisplay)));
            }
        }

        Status = $"Exported {VisibleHistory.Count} history item(s) to {exportPath}.";
        OpenPath(exportPath);
    }

    private async Task RunBusyAsync(
        string busyStatus,
        Func<CancellationToken, Task> operation)
    {
        if (IsBusy)
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        try
        {
            IsBusy = true;
            Status = busyStatus;
            await operation(cancellation.Token);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            CommandOutput = ex.ToString();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetCommandResult(string operation, CommandResult result)
    {
        CommandOutput = string.Join(
            Environment.NewLine,
            $"{operation} exited with code {result.ExitCode}.",
            result.Output,
            result.ErrorOutput);

        Status = result.IsSuccess
            ? $"{operation} completed."
            : $"{operation} failed with exit code {result.ExitCode}.";
    }

    private int StartPageNumber => Math.Max(1, (int)Math.Round(StartPage));

    private int EndPageNumber => Math.Max(0, (int)Math.Round(EndPage));

    private int BookmarkCountLimitNumber => (int)Math.Round(BookmarkCountLimit);

    partial void OnSearchQueryChanged(string value) => RefreshVisibleHistory();

    partial void OnContentTypeFilterChanged(string value)
    {
        RefreshArtistList();
        ApplyPreviewFilters();
        Status = $"Preview filter set to {value}.";
    }

    private async Task LoadHistoryCoreAsync(CancellationToken cancellationToken)
    {
        AccountHistory.Clear();
        foreach (var item in await _database.LoadRecentHistoryAsync(200, cancellationToken))
        {
            AccountHistory.Add(item);
        }

        RefreshVisibleHistory();
        RefreshArtistList();

        if (AccountHistory.Count > 0)
        {
            Status = $"Loaded {AccountHistory.Count} history item(s).";
        }
    }

    private async Task LoadFollowedPreviewsCoreAsync(CancellationToken cancellationToken)
    {
        GalleryFeed = "Followed";
        await LoadGalleryCoreAsync(cancellationToken);
    }

    private async Task LoadGalleryCoreAsync(CancellationToken cancellationToken)
    {
        NewFollowedPreviews.Clear();
        var typeMode = ContentTypeFilter == "Manga" ? "manga" : "illust";
        _previewCache = GalleryFeed switch
        {
            "New" => await _apiClient.GetNewArtworkPreviewsAsync(48, typeMode, ShowR18, cancellationToken),
            "Trending" => await _apiClient.GetRankingPreviewsAsync("daily", typeMode, StartPageNumber, cancellationToken),
            "Popular" => await _apiClient.GetRankingPreviewsAsync("weekly", typeMode, StartPageNumber, cancellationToken),
            _ => await _apiClient.GetNewFollowedPreviewsAsync(StartPageNumber, FeedMode, cancellationToken)
        };

        RefreshArtistList();
        ApplyPreviewFilters();
        OnPropertyChanged(nameof(GalleryTitle));
        OnPropertyChanged(nameof(GalleryStatusLine));
        Status = $"Loaded {NewFollowedPreviews.Count} item(s) for {GalleryFeed}.";
    }

    private bool ShouldShowPreview(PreviewItem item)
    {
        if (!ShowR18 && item.IsR18)
        {
            return false;
        }

        return ContentTypeFilter == "All" ||
               item.ContentType.Contains(ContentTypeFilter, StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldShowArtist(PreviewItem item)
    {
        return SelectedArtist is null ||
               SelectedArtist.Name == item.ArtistName ||
               (!string.IsNullOrWhiteSpace(SelectedArtist.MemberId) && SelectedArtist.MemberId == item.ArtistId);
    }

    private async Task RunDownloadJobAsync(
        string title,
        string description,
        Func<CancellationToken, Task<CommandResult>> operation,
        CancellationToken cancellationToken)
    {
        var job = new DownloadJob(title, description);
        DownloadJobs.Insert(0, job);

        try
        {
            job.Status = "Running";
            job.Progress = 15;

            var result = await operation(cancellationToken);
            job.Progress = 100;
            job.Status = result.IsSuccess ? "Completed" : "Failed";
            job.Details = result.Output + Environment.NewLine + result.ErrorOutput;
            SetCommandResult(title, result);
        }
        catch (Exception ex)
        {
            job.Status = "Failed";
            job.Details = ex.ToString();
            throw;
        }
    }

    private void RefreshVisibleHistory()
    {
        VisibleHistory.Clear();
        var query = SearchQuery.Trim();

        foreach (var item in AccountHistory)
        {
            if (query.Length > 0 &&
                !item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !item.Id.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !(item.MemberId?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) &&
                !(item.LocalPath?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                continue;
            }

            VisibleHistory.Add(item);
        }
    }

    private void RefreshArtistList()
    {
        var selected = SelectedArtist?.MemberId ?? SelectedArtist?.Name;
        FollowedArtists.Clear();

        var artists = _previewCache
            .Where(ShouldShowPreview)
            .GroupBy(item => item.ArtistId ?? item.ArtistName)
            .Select(group =>
            {
                var first = group.First();
                return new FollowedArtist(first.ArtistName, first.ArtistId, group.Count());
            })
            .OrderByDescending(item => item.WorkCount)
            .ThenBy(item => item.Name);

        foreach (var artist in artists)
        {
            FollowedArtists.Add(artist);
            if ((artist.MemberId ?? artist.Name) == selected)
            {
                SelectedArtist = artist;
            }
        }
    }

    private void ApplyPreviewFilters()
    {
        NewFollowedPreviews.Clear();
        foreach (var item in _previewCache)
        {
            if (ShouldShowPreview(item) && ShouldShowArtist(item))
            {
                NewFollowedPreviews.Add(item);
            }
        }

        OnPropertyChanged(nameof(GalleryStatusLine));
    }

    partial void OnGalleryFeedChanged(string value)
    {
        OnPropertyChanged(nameof(GalleryTitle));
        OnPropertyChanged(nameof(GalleryStatusLine));
    }

    partial void OnSelectedArtistChanged(FollowedArtist? value)
    {
        ApplyPreviewFilters();
    }

    [RelayCommand]
    private void ClearSelectedArtist()
    {
        SelectedArtist = null;
    }

    private static void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private static string Csv(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private string FeedMode => ContentTypeFilter switch
    {
        "Illust" => "safe",
        "Manga" => "safe",
        "Ugoira" => "all",
        _ => "all"
    };
}
