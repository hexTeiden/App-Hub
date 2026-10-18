namespace AppHub.Models;

public enum SortMode
{
    Name,
    Recency,
    Publisher,
    Favorite
}

public class AppSettings
{
    public SortMode SortMode { get; set; } = SortMode.Name;

    // Für die Custom-UI-Features vorbereitet, werden noch nicht verwendet:
    public string Theme { get; set; } = "Default";
    public string? BackgroundImagePath { get; set; }
    public string? FontFamily { get; set; }
}
