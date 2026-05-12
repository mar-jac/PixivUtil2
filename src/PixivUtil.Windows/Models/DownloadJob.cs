using CommunityToolkit.Mvvm.ComponentModel;

namespace PixivUtil.Windows.Models;

public sealed partial class DownloadJob : ObservableObject
{
    public DownloadJob(string title, string description)
    {
        Id = Guid.NewGuid().ToString("N");
        Title = title;
        Description = description;
        StartedAt = DateTimeOffset.Now;
    }

    public string Id { get; }

    public string Title { get; }

    public string Description { get; }

    public DateTimeOffset StartedAt { get; }

    [ObservableProperty]
    private string _status = "Queued";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _details = "";

    public string StartedAtDisplay => StartedAt.ToString("g");
}
