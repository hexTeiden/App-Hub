using Avalonia.Media.Imaging;

namespace AppHub.Services.Platform;

public record AppInfo(string Name, string? Publisher);

/// <summary>
/// Alles, was sich zwischen Windows, Linux und macOS unterscheidet.
/// Pro Betriebssystem gibt es eine Implementierung, ausgewählt in <see cref="PlatformServices.Create"/>.
/// </summary>
public interface IPlatformService
{
    /// <summary>Dateimuster für den "Programm hinzufügen"-Dialog, z.B. "*.exe".</summary>
    IReadOnlyList<string> ProgramFilePatterns { get; }

    /// <summary>Liest Anzeigename und (falls möglich) Hersteller aus der Datei.</summary>
    AppInfo ReadInfo(string path);

    /// <summary>Lädt das Icon des Programms, oder null, wenn keins gefunden wird.</summary>
    Bitmap? LoadIcon(string path);

    bool Exists(string path);

    void Launch(string path);
}

public static class PlatformServices
{
    public static IPlatformService Create()
    {
        if (OperatingSystem.IsWindows()) return new WindowsPlatformService();
        if (OperatingSystem.IsMacOS()) return new MacPlatformService();
        return new LinuxPlatformService();
    }
}
