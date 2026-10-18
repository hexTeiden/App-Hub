using AppHub.Models;
using AppHub.Services.Platform;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppHub.ViewModels;

/// <summary>Anzeige-Wrapper um einen <see cref="AppEntry"/>.</summary>
public partial class AppEntryViewModel : ViewModelBase
{
    private readonly IPlatformService _platform;

    [ObservableProperty]
    private Bitmap? _icon;

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

    /// <summary>Prüft den Pfad und lädt das Icon neu.</summary>
    public void Refresh()
    {
        PathMissing = !_platform.Exists(Entry.Path);
        Icon = PathMissing ? null : _platform.LoadIcon(Entry.Path);
        OnPropertyChanged(nameof(Subtitle));
    }
}
