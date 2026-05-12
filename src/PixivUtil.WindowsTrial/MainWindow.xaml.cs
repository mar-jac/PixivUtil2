using System.Diagnostics;
using System.IO;
using System.Windows;

namespace PixivUtil.WindowsTrial;

public partial class MainWindow : Window
{
    private readonly string _appDirectory = AppContext.BaseDirectory;

    public MainWindow()
    {
        InitializeComponent();
        BackendStatus.Text = BackendPath() is null
            ? "PixivUtil2.exe was not found beside this trial shell."
            : "PixivUtil2 backend detected and ready.";

        SeedDesignPreview();
    }

    private void SeedDesignPreview()
    {
        ArtistList.Items.Clear();
        foreach (var artist in new[] { "neco - 234 works", "lack - 186 works", "ASK - 98 works", "redjuice - 58 works", "derori - 49 works" })
        {
            ArtistList.Items.Add(artist);
        }

        Gallery.Items.Clear();
        for (var i = 1; i <= 24; i++)
        {
            Gallery.Items.Add($"Artwork {i:00}  |  Artist {((i % 5) + 1)}  |  {Random.Shared.Next(900, 13000):N0} bookmarks");
        }

        Queue.Items.Add("No active downloads");
    }

    private void PreviewClicked(object sender, RoutedEventArgs e) => SetMode("Preview");

    private void NewClicked(object sender, RoutedEventArgs e) => SetMode("New");

    private void FollowedClicked(object sender, RoutedEventArgs e) => SetMode("Followed");

    private void TrendingClicked(object sender, RoutedEventArgs e) => SetMode("Trending");

    private void PopularClicked(object sender, RoutedEventArgs e) => SetMode("Popular");

    private void SetMode(string mode)
    {
        Status.Text = $"{mode} view loaded. Network-backed previews are implemented in the WinUI branch; this trial shell is bundled to validate Windows app packaging.";
        Output.Text = $"Selected gallery mode: {mode}{Environment.NewLine}{Output.Text}";
    }

    private void DownloadNewFollowedClicked(object sender, RoutedEventArgs e)
    {
        RunBackend(["-s", "8", "-x"], "Download new followed content");
    }

    private void DownloadSelectedClicked(object sender, RoutedEventArgs e)
    {
        if (Gallery.SelectedIndex < 0)
        {
            Status.Text = "Select an artwork first.";
            return;
        }

        var artworkId = (Gallery.SelectedIndex + 1).ToString();
        RunBackend(["-s", "2", "-x", artworkId], $"Download artwork {artworkId}");
    }

    private void DownloadSelectedArtistClicked(object sender, RoutedEventArgs e)
    {
        if (ArtistList.SelectedIndex < 0)
        {
            Status.Text = "Select an artist first.";
            return;
        }

        var memberId = (ArtistList.SelectedIndex + 1).ToString();
        RunBackend(["-s", "1", "-x", memberId], $"Download artist {memberId}");
    }

    private void OpenSelectedClicked(object sender, RoutedEventArgs e)
    {
        var artworkId = Math.Max(Gallery.SelectedIndex + 1, 1).ToString();
        OpenUrl($"https://www.pixiv.net/artworks/{artworkId}");
    }

    private void OpenSelectedArtistClicked(object sender, RoutedEventArgs e)
    {
        var memberId = Math.Max(ArtistList.SelectedIndex + 1, 1).ToString();
        OpenUrl($"https://www.pixiv.net/users/{memberId}");
    }

    private void RunBackend(IReadOnlyList<string> arguments, string title)
    {
        var backend = BackendPath();
        if (backend is null)
        {
            Status.Text = "Backend executable is missing from the bundle.";
            return;
        }

        Queue.Items.Insert(0, $"{title} queued");
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = backend,
                WorkingDirectory = _appDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                Status.Text = "Failed to start backend.";
                return;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Output.Text = $"{title} exited with {process.ExitCode}{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}";
            Queue.Items.Insert(0, $"{title}: {(process.ExitCode == 0 ? "completed" : "failed")}");
            Status.Text = $"{title} finished.";
        }
        catch (Exception ex)
        {
            Output.Text = ex.ToString();
            Status.Text = ex.Message;
        }
    }

    private string? BackendPath()
    {
        var backend = Path.Combine(_appDirectory, "PixivUtil2.exe");
        return File.Exists(backend) ? backend : null;
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
