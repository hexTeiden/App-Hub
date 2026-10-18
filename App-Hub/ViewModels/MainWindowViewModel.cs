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

    /// <summary>0–100 %, nur für Relaxed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TileBackgroundOpacity), nameof(HasTransparentTiles), nameof(TileTransparencyLabel))]
    private double _tileTransparency;

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
        // Alte Einstellung "Favorite" gibt's nicht mehr als Sortierung (jetzt eigener Tab) → zurück auf Name
        _selectedSortOption = SortOptions.FirstOrDefault(o => o.Mode == settings.SortMode) ?? SortOptions[0];
        _selectedThemeOption = ThemeOptions.FirstOrDefault(o => o.Mode.ToString() == settings.Theme) ?? ThemeOptions[0];
        _selectedFontOption = FontOptions.FirstOrDefault(o => o.Family == settings.FontFamily) ?? FontOptions[0];
        _backgroundImage = ImageLoader.TryLoad(settings.BackgroundImagePath);
        _tileTransparency = Math.Clamp(settings.TileTransparency, 0, 100);

        // Oben angepinnt: Favoriten, dann alle Programme, danach die eigenen Gruppen
        Groups.Add(GroupViewModel.Favorites());
        Groups.Add(_allGroup);
        foreach (var group in _data.Groups)
            Groups.Add(GroupViewModel.Custom(group));
        _selectedGroup = _allGroup;

        RefreshMemberships();
        ApplyFilterAndSort();
    }

    /// <summary>Die Apps, die gerade angezeigt werden (gefiltert und sortiert).</summary>
    public ObservableCollection<AppEntryViewModel> Apps { get; } = [];

    /// <summary>Seitenleiste: "★ Favorites", "All programs" + eigene Gruppen.</summary>
    public ObservableCollection<GroupViewModel> Groups { get; } = [];

    private readonly GroupViewModel _allGroup = GroupViewModel.All();

    public IReadOnlyList<SortOption> SortOptions { get; } =
    [
        new(SortMode.Name, "Name"),
        new(SortMode.Recency, "Recently started"),
        new(SortMode.Publisher, "Publisher")
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

    /// <summary>Deckkraft vom Kachel-Hintergrund (Icon, Name und Cover bleiben voll sichtbar).</summary>
    public double TileBackgroundOpacity => 1 - TileTransparency / 100;

    public bool HasTransparentTiles => TileTransparency > 0;

    public string TileTransparencyLabel => $"Tile transparency: {TileTransparency:0} %";

    partial void OnTileTransparencyChanged(double value)
    {
        _data.Settings.TileTransparency = value;
        Save();
    }

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

        if (IsAlreadyAdded(path))
        {
            StatusMessage = "The program has already been added.";
            return;
        }

        AddEntry(path);
        StatusMessage = null;
        Save();
        ApplyFilterAndSort();
    }

    /// <summary>Für Drag &amp; Drop: fügt alle passenden Programme hinzu, der Rest wird übersprungen.</summary>
    public void AddDroppedPaths(IEnumerable<string> paths)
    {
        int added = 0, duplicates = 0, unsupported = 0;

        foreach (var path in paths)
        {
            if (!_platform.Exists(path) || !IsProgramFile(path))
                unsupported++;
            else if (IsAlreadyAdded(path))
                duplicates++;
            else
            {
                AddEntry(path);
                added++;
            }
        }

        var skipped = new List<string>();
        if (duplicates > 0) skipped.Add($"{duplicates} already added");
        if (unsupported > 0) skipped.Add($"{unsupported} not a program");
        StatusMessage = skipped.Count > 0 ? $"Skipped: {string.Join(", ", skipped)}." : null;

        if (added == 0)
            return;

        Save();
        ApplyFilterAndSort();
    }

    /// <summary>
    /// Prüft die Endung gegen <see cref="IPlatformService.ProgramFilePatterns"/> ("*.exe", "*" = alles).
    /// Bei Symlinks zählt auch die Endung vom Ziel, damit z.B. ein Link "Game" auf "Game.exe" klappt.
    /// </summary>
    private bool IsProgramFile(string path)
    {
        var target = LinkResolver.Resolve(path);
        return _platform.ProgramFilePatterns.Any(p => p == "*"
            || (p.StartsWith("*.") && (path.EndsWith(p[1..], StringComparison.OrdinalIgnoreCase)
                                       || target.EndsWith(p[1..], StringComparison.OrdinalIgnoreCase))));
    }

    private bool IsAlreadyAdded(string path) =>
        _allApps.Any(a => string.Equals(a.Path, path, StringComparison.OrdinalIgnoreCase));

    private void AddEntry(string path)
    {
        var info = _platform.ReadInfo(path);
        var entry = new AppEntry { Name = info.Name, Path = path, Publisher = info.Publisher };

        // Wenn gerade eine Gruppe offen ist, landet die neue App direkt darin
        if (SelectedGroup?.Group is { } group)
            entry.GroupIds.Add(group.Id);

        // Im Favoriten-Tab hinzugefügt → direkt Favorit, sonst wäre die App gleich wieder unsichtbar
        if (SelectedGroup?.IsFavorites == true)
            entry.IsFavorite = true;

        var app = new AppEntryViewModel(entry, _platform);
        _data.Apps.Add(entry);
        _allApps.Add(app);
        app.SetMemberships(CreateMemberships(app));
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
        if (SelectedGroup?.IsFavorites == true)
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
        Groups.Add(GroupViewModel.Custom(group));
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
            SelectedGroup = _allGroup;

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

        if (SelectedGroup?.IsFavorites == true)
            query = query.Where(a => a.IsFavorite);
        else if (SelectedGroup?.Group is { } group)
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
