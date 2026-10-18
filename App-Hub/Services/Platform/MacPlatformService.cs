using System.Diagnostics;
using Avalonia.Media.Imaging;

namespace AppHub.Services.Platform;

/// <summary>
/// macOS-Programme sind .app-Ordner (Bundles), deshalb prüft Exists auch auf Ordner.
/// </summary>
public class MacPlatformService : IPlatformService
{
    public IReadOnlyList<string> ProgramFilePatterns { get; } = ["*.app"];

    // TODO: Name und Hersteller aus Contents/Info.plist lesen (CFBundleDisplayName, NSHumanReadableCopyright)
    public AppInfo ReadInfo(string path) =>
        new(Path.GetFileNameWithoutExtension(path.TrimEnd('/')), null);

    // TODO: Icon aus Contents/Resources/*.icns laden (z.B. mit "sips" nach PNG umwandeln)
    public Bitmap? LoadIcon(string path) => null;

    public bool Exists(string path) => Directory.Exists(path) || File.Exists(path);

    public void Launch(string path)
    {
        Process.Start(new ProcessStartInfo("open") { ArgumentList = { path }, UseShellExecute = false });
    }
}
