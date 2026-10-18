using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AppHub.Services;

public interface IFilePickerService
{
    /// <summary>Öffnet einen Datei-Dialog und gibt den lokalen Pfad zurück (null bei Abbruch).</summary>
    Task<string?> PickFileAsync(string title, string filterName, IReadOnlyList<string> patterns);
}

public class FilePickerService(TopLevel topLevel) : IFilePickerService
{
    public async Task<string?> PickFileAsync(string title, string filterName, IReadOnlyList<string> patterns)
    {
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(filterName) { Patterns = patterns }],
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
