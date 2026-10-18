using System.Collections.ObjectModel;
using AppHub.Models;
using AppHub.Services;
using AppHub.Services.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppHub.ViewModels;

public record SortOption(SortMode Mode, string Label);

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
        _selectedSortOption = SortOptions.First(o => o.Mode == _data.Settings.SortMode);

        ApplyFilterAndSort();
    }

    /// <summary>Die Apps, die gerade angezeigt werden (gefiltert und sortiert).</summary>
    public ObservableCollection<AppEntryViewModel> Apps { get; } = [];

    public IReadOnlyList<SortOption> SortOptions { get; } =
    [
        new(SortMode.Name, "Name"),
        new(SortMode.Recency, "Recently started"),
        new(SortMode.Publisher, "Publisher"),
        new(SortMode.Favorite, "Favorites")
    ];

    public bool IsEmpty => _allApps.Count == 0;

    partial void OnSelectedSortOptionChanged(SortOption value)
    {
        _data.Settings.SortMode = value.Mode;
        Save();
        ApplyFilterAndSort();
    }

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

        _data.Apps.Add(entry);
        _allApps.Add(new AppEntryViewModel(entry, _platform));
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
    private void Remove(AppEntryViewModel app)
    {
        _data.Apps.Remove(app.Entry);
        _allApps.Remove(app);
        Save();
        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        IEnumerable<AppEntryViewModel> query = _allApps;

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
