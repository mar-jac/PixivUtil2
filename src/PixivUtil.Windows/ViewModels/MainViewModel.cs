using System.Collections.ObjectModel;
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

    [ObservableProperty]
    private string _pixivCookie = "";

    [ObservableProperty]
    private string _input = "";

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

        DownloadModes = new ObservableCollection<DownloadMode>(DownloadMode.All);
        SelectedDownloadMode = DownloadModes.FirstOrDefault(static mode => mode.Key == "8") ?? DownloadModes.FirstOrDefault();

        var settings = _settingsStore.Load();
        PixivCookie = settings.PixivCookie;
        Status = _downloadBridge.IsAvailable
            ? "Ready"
            : "Ready for previews/history. Download bridge not found yet.";
    }

    public ObservableCollection<DownloadMode> DownloadModes { get; }

    public ObservableCollection<PreviewItem> NewFollowedPreviews { get; } = [];

    public ObservableCollection<DownloadHistoryItem> AccountHistory { get; } = [];

    [RelayCommand]
    private void SaveCookie()
    {
        _settingsStore.SavePixivCookie(PixivCookie.Trim());
        Status = "Pixiv cookie saved to config.ini.";
    }

    [RelayCommand]
    private async Task RefreshHistoryAsync()
    {
        await RunBusyAsync("Loading account history...", async cancellationToken =>
        {
            await LoadHistoryCoreAsync(cancellationToken);
        });
    }

    [RelayCommand]
    private async Task PreviewNewFollowedAsync()
    {
        await RunBusyAsync("Loading followed-artist previews...", async cancellationToken =>
        {
            NewFollowedPreviews.Clear();
            foreach (var item in await _apiClient.GetNewFollowedPreviewsAsync(StartPageNumber, cancellationToken: cancellationToken))
            {
                NewFollowedPreviews.Add(item);
            }

            Status = $"Loaded {NewFollowedPreviews.Count} followed-artist preview(s).";
        });
    }

    [RelayCommand]
    private async Task DownloadNewFollowedAsync()
    {
        await RunBusyAsync("Downloading new followed-artist content...", async cancellationToken =>
        {
            var result = await _downloadBridge.DownloadNewFollowedContentAsync(
                StartPageNumber,
                EndPageNumber,
                BookmarkCountLimitNumber,
                cancellationToken);

            SetCommandResult("New followed-artist download", result);
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

            var result = await _downloadBridge.DownloadAsync(request, cancellationToken);
            SetCommandResult(SelectedDownloadMode.Name, result);
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

    private async Task LoadHistoryCoreAsync(CancellationToken cancellationToken)
    {
        AccountHistory.Clear();
        foreach (var item in await _database.LoadRecentHistoryAsync(200, cancellationToken))
        {
            AccountHistory.Add(item);
        }

        if (AccountHistory.Count > 0)
        {
            Status = $"Loaded {AccountHistory.Count} history item(s).";
        }
    }
}
