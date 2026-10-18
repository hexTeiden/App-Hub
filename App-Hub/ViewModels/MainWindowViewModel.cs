using System.Collections.ObjectModel;
using AppHub.Models;
using AppHub.Services;
using AppHub.Services.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppHub.ViewModels;

public record SortOption(SortMode Mode, string Label);
public record ThemeOption(ThemeMode Mode, string Label);

/// <summary>Schriftart zur Auswahl. <see cref="Family"/> null = Standard-Schrift.</summary>
public record FontOption(string Label, string? Family);

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly JsonDataStore _store;
    private readonly IPlatformService _platform;
    private readonly IFilePickerService _filePicker;
    private readonly AppData _data;

    /// <summary>Alle Apps, unabhängig von Suche und Sortierung.</summary>
    private readonly List<AppEntryViewModel> _allApps;

    [ObservableProperty]
    private SortOption _selectedSortOption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRelaxed))]
    private ThemeOption _selectedThemeOption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentFontFamily))]
    private FontOption _selectedFontOption;

    /// <summary>Ausgewählte Gruppe in der Seitenleiste.</summary>
    [ObservableProperty]
    private GroupViewModel? _selectedGroup;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateGroupCommand))]
    private string _newGroupName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackgroundImage))]
    private Bitmap? _backgroundImage;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string? _statusMessage;

    public MainWindowViewModel(JsonDataStore store, IPlatformService platform, IFilePickerService filePicker)
    {
        _store = store;
        _platform = platform;
        _filePicker = filePicker;

        _data = _store.Load();
        _allApps = _data.Apps.Select(a => new AppEntryViewModel(a, _platform)).ToList();

        var settings = _data.Settings;
        _selectedSortOption = SortOptions.First(o => o.Mode == settings.SortMode);
        _selectedThemeOption = ThemeOptions.FirstOrDefault(o => o.Mode.ToString() == settings.Theme) ?? ThemeOptions[0];
        _selectedFontOption = FontOptions.FirstOrDefault(o => o.Family == settings.FontFamily) ?? FontOptions[0];
        _backgroundImage = ImageLoader.TryLoad(settings.BackgroundImagePath);

        Groups.Add(new GroupViewModel(null));
        foreach (var group in _data.Groups)
            Groups.Add(new GroupViewModel(group));
        _selectedGroup = Groups[0];

        RefreshMemberships();
        ApplyFilterAndSort();
    }

    /// <summary>Die Apps, die gerade angezeigt werden (gefiltert und sortiert).</summary>
    public ObservableCollection<AppEntryViewModel> Apps { get; } = [];

    /// <summary>Seitenleiste: "All programs" + eigene Gruppen.</summary>
    public ObservableCollection<GroupViewModel> Groups { get; } = [];

    public IReadOnlyList<SortOption> SortOptions { get; } =
    [
        new(SortMode.Name, "Name"),
        new(SortMode.Recency, "Recently started"),
        new(SortMode.Publisher, "Publisher"),
        new(SortMode.Favorite, "Favorites")
    ];

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(ThemeMode.Relaxed, "Relaxed"),
        new(ThemeMode.Dense, "Dense")
    ];

    public IReadOnlyList<FontOption> FontOptions { get; } =
    [
        new("Default", null),
        new("Iosevka Nerd Font", "Iosevka Nerd Font"),
        new("JetBrains Mono", "JetBrains Mono"),
        new("Arial", "Arial"),
        new("Times New Roman", "Times New Roman"),
        new("Chiller", "Chiller")
    ];

    public bool IsEmpty => _allApps.Count == 0;

    /// <summary>Apps sind da, aber Filter/Gruppe/Favoriten zeigen keine an.</summary>
    public bool IsFilteredEmpty => !IsEmpty && Apps.Count == 0;

    /// <summary>Relaxed = Kachel-Raster, Dense = kompakte Liste</summary>
    public bool IsRelaxed => SelectedThemeOption.Mode == ThemeMode.Relaxed;

    public FontFamily CurrentFontFamily =>
        SelectedFontOption.Family is { } family ? new FontFamily(family) : FontFamily.Default;

    public bool HasBackgroundImage => BackgroundImage is not null;

    partial void OnSelectedSortOptionChanged(SortOption value)
    {
        _data.Settings.SortMode = value.Mode;
        Save();
        ApplyFilterAndSort();
    }

    partial void OnSelectedThemeOptionChanged(ThemeOption value)
    {
        _data.Settings.Theme = value.Mode.ToString();
        Save();
    }

    partial void OnSelectedFontOptionChanged(FontOption value)
    {
        _data.Settings.FontFamily = value.Family;
        Save();
    }

    partial void OnSelectedGroupChanged(GroupViewModel? value) => ApplyFilterAndSort();

    partial void OnSearchTextChanged(string value) => ApplyFilterAndSort();

    [RelayCommand]
    private async Task AddApp()
    {
        var path = await _filePicker.PickFileAsync("Add programs", "Programs", _platform.ProgramFilePatterns);
        if (path is null)
            return;

        if (_allApps.Any(a => string.Equals(a.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "The program has already been added.";
            return;
        }

        var info = _platform.ReadInfo(path);
        var entry = new AppEntry { Name = info.Name, Path = path, Publisher = info.Publisher };

        // Wenn gerade eine Gruppe offen ist, landet die neue App direkt darin
        if (SelectedGroup?.Group is { } group)
            entry.GroupIds.Add(group.Id);

        var app = new AppEntryViewModel(entry, _platform);
        _data.Apps.Add(entry);
        _allApps.Add(app);
        app.SetMemberships(CreateMemberships(app));
        StatusMessage = null;
        Save();
        ApplyFilterAndSort();
    }

    [RelayCommand]
    private void Launch(AppEntryViewModel app)
    {
        app.Refresh();
        if (app.PathMissing)
        {
            StatusMessage = $"„{app.Name}“ has not been found. Change path?";
            return;
        }

        try
        {
            _platform.Launch(app.Path);
            app.MarkLaunched();
            StatusMessage = null;
            Save();
            if (SelectedSortOption.Mode == SortMode.Recency)
                ApplyFilterAndSort();
        }
        catch (Exception ex)
        {
            StatusMessage = $"„{app.Name}“ couldn't start: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleFavorite(AppEntryViewModel app)
    {
        app.ToggleFavorite();
        Save();
        if (SelectedSortOption.Mode == SortMode.Favorite)
            ApplyFilterAndSort();
    }

    [RelayCommand]
    private async Task ChangePath(AppEntryViewModel app)
    {
        var path = await _filePicker.PickFileAsync($"New path for „{app.Name}“", "programs", _platform.ProgramFilePatterns);
        if (path is null)
            return;

        app.ChangePath(path, _platform.ReadInfo(path));
        StatusMessage = null;
        Save();
        ApplyFilterAndSort();
    }

    [RelayCommand]
    private async Task ChangeImage(AppEntryViewModel app)
    {
        var path = await _filePicker.PickFileAsync($"Image for „{app.Name}“", "Images", ImageLoader.ImageFilePatterns);
        if (path is null)
            return;

        if (ImageLoader.TryLoad(path) is null)
        {
            StatusMessage = "The image couldn't be loaded.";
            return;
        }

        app.ChangeImage(path);
        StatusMessage = null;
        Save();
    }

    [RelayCommand]
    private void ResetImage(AppEntryViewModel app)
    {
        app.ChangeImage(null);
        Save();
    }

    [RelayCommand]
    private void Remove(AppEntryViewModel app)
    {
        _data.Apps.Remove(app.Entry);
        _allApps.Remove(app);
        Save();
        ApplyFilterAndSort();
    }

    private bool CanCreateGroup() => !string.IsNullOrWhiteSpace(NewGroupName);

    [RelayCommand(CanExecute = nameof(CanCreateGroup))]
    private void CreateGroup()
    {
        var name = NewGroupName.Trim();
        if (_data.Groups.Any(g => string.Equals(g.Name, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            StatusMessage = $"A group called „{name}“ already exists.";
            return;
        }

        var group = new AppGroup { Name = name };
        _data.Groups.Add(group);
        Groups.Add(new GroupViewModel(group));
        NewGroupName = "";
        StatusMessage = null;
        RefreshMemberships();
        Save();
    }

    [RelayCommand]
    private void DeleteGroup(GroupViewModel groupVm)
    {
        if (groupVm.Group is not { } group)
            return;

        _data.Groups.Remove(group);
        foreach (var entry in _data.Apps)
            entry.GroupIds.Remove(group.Id);

        Groups.Remove(groupVm);
        if (SelectedGroup is null || SelectedGroup == groupVm)
            SelectedGroup = Groups[0];

        RefreshMemberships();
        Save();
        ApplyFilterAndSort();
    }

    [RelayCommand]
    private async Task ChangeBackground()
    {
        var path = await _filePicker.PickFileAsync("Background image", "Images", ImageLoader.ImageFilePatterns);
        if (path is null)
            return;

        var image = ImageLoader.TryLoad(path);
        if (image is null)
        {
            StatusMessage = "The image couldn't be loaded.";
            return;
        }

        BackgroundImage = image;
        _data.Settings.BackgroundImagePath = path;
        StatusMessage = null;
        Save();
    }

    [RelayCommand]
    private void ResetBackground()
    {
        BackgroundImage = null;
        _data.Settings.BackgroundImagePath = null;
        Save();
    }

    /// <summary>Baut die Gruppen-Checkboxen aller Apps neu (nach Anlegen/Löschen einer Gruppe).</summary>
    private void RefreshMemberships()
    {
        foreach (var app in _allApps)
            app.SetMemberships(CreateMemberships(app));
    }

    private IEnumerable<GroupMembershipViewModel> CreateMemberships(AppEntryViewModel app) =>
        _data.Groups.Select(g => new GroupMembershipViewModel(g, app.Entry.GroupIds.Contains(g.Id),
            m => OnMembershipChanged(app, m)));

    private void OnMembershipChanged(AppEntryViewModel app, GroupMembershipViewModel membership)
    {
        if (membership.IsMember)
        {
            if (!app.Entry.GroupIds.Contains(membership.Group.Id))
                app.Entry.GroupIds.Add(membership.Group.Id);
        }
        else
        {
            app.Entry.GroupIds.Remove(membership.Group.Id);
        }

        Save();
        if (SelectedGroup?.Group == membership.Group)
            ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        IEnumerable<AppEntryViewModel> query = _allApps;

        if (SelectedGroup?.Group is { } group)
            query = query.Where(a => a.Entry.GroupIds.Contains(group.Id));

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(a =>
                a.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (a.Publisher?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        query = SelectedSortOption.Mode switch
        {
            SortMode.Recency => query
                .OrderByDescending(a => a.LastLaunchedAt ?? DateTime.MinValue)
                .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase),
            SortMode.Publisher => query
                .OrderBy(a => string.IsNullOrEmpty(a.Publisher)) // Apps ohne Hersteller ans Ende
                .ThenBy(a => a.Publisher, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase),
            SortMode.Favorite => query
                .Where(a => a.IsFavorite) // Nur Favoriten anzeigen
                .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => query.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase),
        };

        Apps.Clear();
        foreach (var app in query)
            Apps.Add(app);

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsFilteredEmpty));
    }

    private void Save()
    {
        try
        {
            _store.Save(_data);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Saving failed: {ex.Message}";
        }
    }
}
