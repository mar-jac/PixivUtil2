using CommunityToolkit.Mvvm.ComponentModel;

namespace PixivUtil.Windows.Models;

public sealed partial class FollowedArtist : ObservableObject
{
    public FollowedArtist(string name, string? memberId, int workCount)
    {
        Name = name;
        MemberId = memberId;
        WorkCount = workCount;
    }

    public string Name { get; }

    public string? MemberId { get; }

    public int WorkCount { get; }

    public string WorkCountDisplay => $"{WorkCount:N0} works";

    [ObservableProperty]
    private bool _isSelected = true;
}
