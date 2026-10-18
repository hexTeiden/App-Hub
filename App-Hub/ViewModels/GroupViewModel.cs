using AppHub.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppHub.ViewModels;

/// <summary>
/// Eintrag in der Seitenleiste: entweder eine eigene Gruppe, oder einer der fest angepinnten
/// Einträge oben ("★ Favorites", "All programs"), dann ist <see cref="Group"/> null.
/// </summary>
public class GroupViewModel
{
    private GroupViewModel(AppGroup? group, bool isFavorites)
    {
        Group = group;
        IsFavorites = isFavorites;
    }

    public static GroupViewModel All() => new(null, false);
    public static GroupViewModel Favorites() => new(null, true);
    public static GroupViewModel Custom(AppGroup group) => new(group, false);

    public AppGroup? Group { get; }
    public bool IsFavorites { get; }

    public string Name => Group?.Name ?? (IsFavorites ? "★ Favorites" : "All programs");

    /// <summary>Nur eigene Gruppen kann man löschen, die angepinnten nicht.</summary>
    public bool CanDelete => Group is not null;

    public bool IsPinned => Group is null;

    /// <summary>Trennlinie unter dem letzten angepinnten Eintrag ("All programs").</summary>
    public bool ShowSeparator => Group is null && !IsFavorites;
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
