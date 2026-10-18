using System.Diagnostics;
using System.Runtime.Versioning;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace AppHub.Services.Platform;

[SupportedOSPlatform("windows")]
public class WindowsPlatformService : IPlatformService
{
    public IReadOnlyList<string> ProgramFilePatterns { get; } = ["*.exe", "*.lnk", "*.bat", "*.cmd"];

    public AppInfo ReadInfo(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        string? publisher = null;

        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            if (!string.IsNullOrWhiteSpace(info.FileDescription))
                name = info.FileDescription.Trim();
            if (!string.IsNullOrWhiteSpace(info.CompanyName))
                publisher = info.CompanyName.Trim();
        }

        return new AppInfo(name, publisher);
    }

    public AvaloniaBitmap? LoadIcon(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null)
                return null;

            using var bitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            return new AvaloniaBitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public bool Exists(string path) => File.Exists(path);

    public void Launch(string path)
    {
        // UseShellExecute = true: funktioniert auch mit .lnk-Verknüpfungen und .bat-Dateien
        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(path) ?? "",
        });
    }
}
