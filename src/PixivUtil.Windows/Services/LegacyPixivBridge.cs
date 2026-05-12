using System.Diagnostics;
using System.Text;
using PixivUtil.Windows.Models;

namespace PixivUtil.Windows.Services;

public sealed class LegacyPixivBridge(AppPaths paths)
{
    public bool IsAvailable => paths.LegacyExecutablePath is not null;

    public Task<CommandResult> DownloadAsync(
        DownloadRequest request,
        CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "-s", request.Mode.Key, "-x" };

        AddModeInput(args, request);

        if (request.StartPage > 1)
        {
            args.AddRange(["--sp", request.StartPage.ToString()]);
        }

        if (request.EndPage > 0)
        {
            args.AddRange(["--ep", request.EndPage.ToString()]);
        }

        if (request.IncludeSketch)
        {
            args.Add("--include_sketch");
        }

        if (request.UseWildcardTags)
        {
            args.Add("--use_wildcard_tag");
        }

        if (request.BookmarkCountLimit >= 0)
        {
            args.AddRange(["--bookmark_count_limit", request.BookmarkCountLimit.ToString()]);
        }

        return RunAsync(args, cancellationToken);
    }

    public Task<CommandResult> DownloadNewFollowedContentAsync(
        int startPage,
        int endPage,
        int bookmarkCountLimit,
        CancellationToken cancellationToken = default)
    {
        var mode = DownloadMode.All.First(static item => item.Key == "8");
        return DownloadAsync(
            new DownloadRequest(
                mode,
                Input: "",
                StartPage: startPage,
                EndPage: endPage,
                IncludeSketch: false,
                UseWildcardTags: false,
                BookmarkCountLimit: bookmarkCountLimit),
            cancellationToken);
    }

    public async Task<CommandResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var executable = paths.LegacyExecutablePath
            ?? throw new FileNotFoundException("PixivUtil2.exe or PixivUtil2.py was not found.", paths.AppDirectory);

        var startInfo = new ProcessStartInfo
        {
            WorkingDirectory = paths.AppDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (Path.GetExtension(executable).Equals(".py", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName = "python";
            startInfo.ArgumentList.Add(executable);
        }
        else
        {
            startInfo.FileName = executable;
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start PixivUtil process.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        return new CommandResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }

    private static void AddModeInput(List<string> args, DownloadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return;
        }

        if (request.Mode.Key is "4" or "7")
        {
            args.AddRange(["-f", request.Input]);
            return;
        }

        if (request.Mode.Key is "b")
        {
            args.AddRange(["--batch_file", request.Input]);
            return;
        }

        args.AddRange(SplitCommandInput(request.Input));
    }

    private static IEnumerable<string> SplitCommandInput(string input)
    {
        var builder = new StringBuilder();
        var inQuotes = false;

        foreach (var character in input)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (builder.Length > 0)
                {
                    yield return builder.ToString();
                    builder.Clear();
                }
                continue;
            }

            builder.Append(character);
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }
}
