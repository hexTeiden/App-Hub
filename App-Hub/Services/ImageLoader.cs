using Avalonia.Media.Imaging;

namespace AppHub.Services;

public static class ImageLoader
{
    public static IReadOnlyList<string> ImageFilePatterns { get; } = ["*.png", "*.jpg", "*.jpeg"];

    /// <summary>Lädt ein Bild von der Platte, null wenn es fehlt oder kaputt ist.</summary>
    public static Bitmap? TryLoad(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            return new Bitmap(path);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
