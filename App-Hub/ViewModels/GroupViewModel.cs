using AppHub.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppHub.ViewModels;

/// <summary>Eintrag in der Seitenleiste. <see cref="Group"/> ist null bei "All programs".</summary>
public class GroupViewModel(AppGroup? group)
{
    public AppGroup? Group { get; } = group;
    public string Name => Group?.Name ?? "All programs";
    public bool CanDelete => Group is not null;
}

/// <summary>Checkbox "App ist in Gruppe X" im Groups-Menü einer App.</summary>
public partial class GroupMembershipViewModel : ViewModelBase
{
    private readonly Action<GroupMembershipViewModel> _onChanged;

    [ObservableProperty]
    private bool _isMember;

    public GroupMembershipViewModel(AppGroup group, bool isMember, Action<GroupMembershipViewModel> onChanged)
    {
        Group = group;
        _isMember = isMember;
        _onChanged = onChanged;
    }

    public AppGroup Group { get; }
    public string Name => Group.Name;

    partial void OnIsMemberChanged(bool value) => _onChanged(this);
}
