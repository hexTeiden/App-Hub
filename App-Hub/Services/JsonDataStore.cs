using System.Text.Json;
using System.Text.Json.Serialization;
using AppHub.Models;

namespace AppHub.Services;

/// <summary>
/// Speichert die App-Daten als JSON im Benutzerordner:
/// Windows: %LOCALAPPDATA%\AppHub, Linux: ~/.local/share/AppHub, macOS: ~/Library/Application Support/AppHub
/// </summary>
public class JsonDataStore
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppHub");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath = Path.Combine(DataDirectory, "data.json");

    public AppData Load()
    {
        if (!File.Exists(_filePath))
            return new AppData();

        try
        {
            return JsonSerializer.Deserialize<AppData>(File.ReadAllText(_filePath), Options) ?? new AppData();
        }
        catch (JsonException)
        {
            // Kaputte Datei nicht überschreiben, sondern zur Seite legen
            File.Copy(_filePath, _filePath + $".broken-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);
            return new AppData();
        }
    }

    public void Save(AppData data)
    {
        Directory.CreateDirectory(DataDirectory);

        // Erst in eine Temp-Datei schreiben, damit bei einem Absturz data.json nicht halb leer ist
        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(data, Options));
        File.Move(tempPath, _filePath, overwrite: true);
    }
}
