namespace AppHub.Models;

/// <summary>Alles, was in data.json gespeichert wird.</summary>
public class AppData
{
    public int Version { get; set; } = 1;
    public List<AppEntry> Apps { get; set; } = [];
    public List<AppGroup> Groups { get; set; } = [];
    public AppSettings Settings { get; set; } = new();
}
