using System.Collections.ObjectModel;
using AppHub.Models;
using AppHub.Services;
using AppHub.Services.Platform;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppHub.ViewModels;

/// <summary>Anzeige-Wrapper um einen <see cref="AppEntry"/>.</summary>
public partial class AppEntryViewModel : ViewModelBase
{
    private readonly IPlatformService _platform;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayIcon))]
    private Bitmap? _icon;

    /// <summary>Eigenes Bild aus <see cref="AppEntry.ImagePath"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayIcon), nameof(HasCustomImage))]
    private Bitmap? _customImage;

    [ObservableProperty]
    private bool _pathMissing;

    public AppEntryViewModel(AppEntry entry, IPlatformService platform)
    {
        Entry = entry;
        _platform = platform;
        Refresh();
    }

    public AppEntry Entry { get; }

    public string Name => Entry.Name;
    public string Path => Entry.Path;
    public string? Publisher => Entry.Publisher;
    public DateTime? LastLaunchedAt => Entry.LastLaunchedAt;
    public bool IsFavorite => Entry.IsFavorite;

    /// <summary>Leerer Stern = kein Favorit, voller Stern = Favorit</summary>
    public string FavoriteIcon => IsFavorite ? "★" : "☆";

    public bool HasCustomImage => CustomImage is not null;

    /// <summary>Für die Liste: eigenes Bild, sonst Icon aus der Exe.</summary>
    public Bitmap? DisplayIcon => CustomImage ?? Icon;

    /// <summary>Checkboxen fürs Groups-Menü, werden vom MainWindowViewModel befüllt.</summary>
    public ObservableCollection<GroupMembershipViewModel> Memberships { get; } = [];

    public bool HasGroups => Memberships.Count > 0;

    /// <summary>Zweite Zeile in der Liste: Hersteller · zuletzt gestartet</summary>
    public string Subtitle
    {
        get
        {
            var parts = new List<string>();
            if (PathMissing) parts.Add("⚠ path not found!");
            if (!string.IsNullOrEmpty(Publisher)) parts.Add(Publisher);
            if (string.IsNullOrEmpty(Publisher)) parts.Add("No publisher provided");
            parts.Add(LastLaunchedAt is { } t ? $"Last started: {t:dd.MM.yyyy HH:mm}" : "Not started before");
            return string.Join(" · ", parts);
        }
    }

    public void MarkLaunched()
    {
        Entry.LastLaunchedAt = DateTime.Now;
        OnPropertyChanged(nameof(LastLaunchedAt));
        OnPropertyChanged(nameof(Subtitle));
    }

    public void ToggleFavorite()
    {
        Entry.IsFavorite = !Entry.IsFavorite;
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(FavoriteIcon));
    }

    public void ChangePath(string newPath, AppInfo info)
    {
        Entry.Path = newPath;
        Entry.Publisher = info.Publisher;
        Refresh();
        OnPropertyChanged(nameof(Path));
        OnPropertyChanged(nameof(Publisher));
    }

    /// <summary>null setzt das Bild zurück aufs Exe-Icon.</summary>
    public void ChangeImage(string? imagePath)
    {
        Entry.ImagePath = imagePath;
        CustomImage = ImageLoader.TryLoad(imagePath);
    }

    public void SetMemberships(IEnumerable<GroupMembershipViewModel> memberships)
    {
        Memberships.Clear();
        foreach (var m in memberships)
            Memberships.Add(m);
        OnPropertyChanged(nameof(HasGroups));
    }

    /// <summary>Prüft den Pfad und lädt Icon und eigenes Bild neu.</summary>
    public void Refresh()
    {
        PathMissing = !_platform.Exists(Entry.Path);
        Icon = PathMissing ? null : _platform.LoadIcon(Entry.Path);
        CustomImage = ImageLoader.TryLoad(Entry.ImagePath);
        OnPropertyChanged(nameof(Subtitle));
    }
}
