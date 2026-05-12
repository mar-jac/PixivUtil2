namespace PixivUtil.Windows.Models;

public sealed record CommandResult(
    int ExitCode,
    string Output,
    string ErrorOutput)
{
    public bool IsSuccess => ExitCode == 0;
}
