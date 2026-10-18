namespace AppHub.Models;

public enum SortMode
{
    Name,
    Recency,
    Publisher,
    Favorite
}

public enum ThemeMode
{
    Relaxed,
    Dense
}

public class AppSettings
{
    public SortMode SortMode { get; set; } = SortMode.Name;

    /// <summary>Name eines <see cref="ThemeMode"/>. Als String, damit alte Werte wie "Default" data.json nicht kaputt machen.</summary>
    public string Theme { get; set; } = "Relaxed";
    public string? BackgroundImagePath { get; set; }
    public string? FontFamily { get; set; }

    /// <summary>Durchsichtigkeit der Kacheln im Relaxed-Theme in Prozent (0 = undurchsichtig).</summary>
    public double TileTransparency { get; set; }
}
