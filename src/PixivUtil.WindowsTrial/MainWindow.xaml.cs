using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PixivUtil.WindowsTrial;

public partial class MainWindow : Window
{
    private readonly string _appDirectory = AppContext.BaseDirectory;

    public ObservableCollection<ArtistCard> Artists { get; } = [];

    public ObservableCollection<ArtworkCard> GalleryItems { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        BackendStatus.Text = BackendPath() is null
            ? "PixivUtil2.exe was not found beside this trial shell."
            : "PixivUtil2 backend detected and ready.";

        SeedDesignPreview();
    }

    private void SeedDesignPreview()
    {
        Artists.Clear();
        Artists.Add(new ArtistCard("neco", 234, Palette("#C984D9", "#75B7FF"), true));
        Artists.Add(new ArtistCard("lack", 186, Palette("#8AA8FF", "#4E6BD8"), true));
        Artists.Add(new ArtistCard("ASK", 98, Palette("#EAA15F", "#AC6CF6"), false));
        Artists.Add(new ArtistCard("redjuice", 58, Palette("#6DFFDB", "#4B85FF"), false));
        Artists.Add(new ArtistCard("derori", 49, Palette("#F96B9B", "#7D5CFF"), false));

        GalleryItems.Clear();
        var artists = Artists.Select(artist => artist.Name).ToArray();
        var titles = new[]
        {
            "twilight", "sky bloom", "archive signal", "rainy step", "blue station", "cloudline",
            "scarlet orbit", "distant tower", "night glass", "fragment", "summer pulse", "mirror city",
            "field notes", "quiet shore", "paper moon", "afterimage", "window light", "star map"
        };
        var gradients = new[]
        {
            Palette("#284A8A", "#ED7B9B"),
            Palette("#2176FF", "#B4ECFF"),
            Palette("#202838", "#D65A7E"),
            Palette("#3932A8", "#FF84D7"),
            Palette("#0F8A9D", "#91F6FF"),
            Palette("#2151A5", "#F5B36A"),
            Palette("#3A1F3F", "#FF6B6B"),
            Palette("#203459", "#D77943")
        };

        for (var i = 0; i < 30; i++)
        {
            GalleryItems.Add(new ArtworkCard(
                (i + 1).ToString(),
                titles[i % titles.Length],
                artists[i % artists.Length],
                $"{Random.Shared.Next(900, 13000):N0}",
                $"{(i % 5) + 1}/{((i % 3) + 1) * 4}",
                i % 4 == 0 ? "#manga #ugoira #new" : "#illust #followed #safe",
                gradients[i % gradients.Length]));
        }

        Queue.Items.Add("No active downloads");
        Gallery.SelectedIndex = 0;
    }

    private void PreviewClicked(object sender, RoutedEventArgs e) => SetMode("Preview");

    private void NewClicked(object sender, RoutedEventArgs e) => SetMode("New");

    private void FollowedClicked(object sender, RoutedEventArgs e) => SetMode("Followed");

    private void TrendingClicked(object sender, RoutedEventArgs e) => SetMode("Trending");

    private void PopularClicked(object sender, RoutedEventArgs e) => SetMode("Popular");

    private void SetMode(string mode)
    {
        Status.Text = $"{mode} view loaded.";
        Output.Text = $"Selected gallery mode: {mode}{Environment.NewLine}{Output.Text}";
    }

    private void DownloadNewFollowedClicked(object sender, RoutedEventArgs e)
    {
        RunBackend(["-s", "8", "-x"], "Download new followed content");
    }

    private void DownloadSelectedClicked(object sender, RoutedEventArgs e)
    {
        if (Gallery.SelectedItem is not ArtworkCard artwork)
        {
            Status.Text = "Select an artwork first.";
            return;
        }

        RunBackend(["-s", "2", "-x", artwork.Id], $"Download artwork {artwork.Id}");
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
        var artworkId = Gallery.SelectedItem is ArtworkCard artwork
            ? artwork.Id
            : Math.Max(Gallery.SelectedIndex + 1, 1).ToString();
        OpenUrl($"https://www.pixiv.net/artworks/{artworkId}");
    }

    private void OpenSelectedArtistClicked(object sender, RoutedEventArgs e)
    {
        var memberId = Math.Max(ArtistList.SelectedIndex + 1, 1).ToString();
        OpenUrl($"https://www.pixiv.net/users/{memberId}");
    }

    private void GallerySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Gallery.SelectedItem is not ArtworkCard artwork)
        {
            return;
        }

        SelectedTitle.Text = artwork.Title;
        SelectedArtist.Text = artwork.Artist;
        InspectorPreview.Background = artwork.ThumbnailBrush;
    }

    private void ArtistSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ArtistList.SelectedItem is ArtistCard artist)
        {
            Status.Text = $"Filtering preview by {artist.Name}.";
        }
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
        if (File.Exists(backend))
        {
            return backend;
        }

        return ExtractEmbeddedBackend();
    }

    private static string? ExtractEmbeddedBackend()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("PixivUtil2.exe", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            return null;
        }

        var backendDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PixivUtil.WindowsTrial",
            "backend");
        Directory.CreateDirectory(backendDirectory);

        var backendPath = Path.Combine(backendDirectory, "PixivUtil2.exe");
        using var resource = assembly.GetManifestResourceStream(resourceName);
        if (resource is null)
        {
            return null;
        }

        using var file = File.Create(backendPath);
        resource.CopyTo(file);
        return backendPath;
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private static LinearGradientBrush Palette(string start, string end)
    {
        return new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(start),
            (Color)ColorConverter.ConvertFromString(end),
            45);
    }
}

public sealed class ArtistCard(string name, int workCount, Brush avatarBrush, bool isSelected)
{
    public string Name { get; } = name;

    public int WorkCount { get; } = workCount;

    public string WorkCountText => $"{WorkCount:N0} works";

    public Brush AvatarBrush { get; } = avatarBrush;

    public bool IsSelected { get; set; } = isSelected;
}

public sealed class ArtworkCard(
    string id,
    string title,
    string artist,
    string bookmarks,
    string pageText,
    string tags,
    Brush thumbnailBrush)
{
    public string Id { get; } = id;

    public string Title { get; } = title;

    public string Artist { get; } = artist;

    public string BookmarkText => bookmarks;

    public string PageText { get; } = pageText;

    public string Tags { get; } = tags;

    public Brush ThumbnailBrush { get; } = thumbnailBrush;

    public bool IsSelected { get; set; }
}
