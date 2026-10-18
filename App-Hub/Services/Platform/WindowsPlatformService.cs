using System.Diagnostics;
using System.Runtime.Versioning;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace AppHub.Services.Platform;

[SupportedOSPlatform("windows")]
public class WindowsPlatformService : IPlatformService
{
    // .url = Internet-Verknüpfungen, so legen z.B. Steam und Epic ihre Spiele auf den Desktop
    public IReadOnlyList<string> ProgramFilePatterns { get; } = ["*.exe", "*.lnk", "*.url", "*.bat", "*.cmd"];

    public AppInfo ReadInfo(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        string? publisher = null;

        // Bei einem Symlink die Infos aus der Datei lesen, auf die er zeigt
        var target = LinkResolver.Resolve(path);

        if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            var info = FileVersionInfo.GetVersionInfo(target);
            if (!string.IsNullOrWhiteSpace(info.FileDescription))
                name = info.FileDescription.Trim();
            if (!string.IsNullOrWhiteSpace(info.CompanyName))
                publisher = info.CompanyName.Trim();
        }
        else if (target.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
        {
            publisher = ReadUrlShortcut(target).Url switch
            {
                { } url when url.StartsWith("steam://", StringComparison.OrdinalIgnoreCase) => "Steam",
                { } url when url.StartsWith("com.epicgames.launcher://", StringComparison.OrdinalIgnoreCase) => "Epic Games",
                _ => null,
            };
        }

        return new AppInfo(name, publisher);
    }

    public AvaloniaBitmap? LoadIcon(string path)
    {
        if (!LinkResolver.TargetExists(path))
            return null;

        try
        {
            var target = LinkResolver.Resolve(path);
            using var icon = target.EndsWith(".url", StringComparison.OrdinalIgnoreCase)
                ? LoadUrlShortcutIcon(target)
                : System.Drawing.Icon.ExtractAssociatedIcon(target);
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

    public bool Exists(string path) => LinkResolver.TargetExists(path);

    public void Launch(string path)
    {
        // UseShellExecute = true: funktioniert auch mit .lnk/.url-Verknüpfungen, .bat-Dateien und Symlinks.
        // Arbeitsordner ist der Ordner vom echten Programm, nicht der vom Symlink (viele Spiele brauchen das).
        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(LinkResolver.Resolve(path)) ?? "",
        });
    }

    /// <summary>Liest URL und IconFile aus einer .url-Datei (INI-Format).</summary>
    private static (string? Url, string? IconFile) ReadUrlShortcut(string path)
    {
        string? url = null, iconFile = null;
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                url = line[4..].Trim();
            else if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                iconFile = line[9..].Trim();
        }
        return (url, iconFile);
    }

    /// <summary>Icon aus dem IconFile der Verknüpfung, sonst das Standard-Icon für .url.</summary>
    private static System.Drawing.Icon? LoadUrlShortcutIcon(string path)
    {
        var iconFile = ReadUrlShortcut(path).IconFile;
        if (iconFile is null || !File.Exists(iconFile))
            return System.Drawing.Icon.ExtractAssociatedIcon(path);

        return iconFile.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
            ? new System.Drawing.Icon(iconFile, 256, 256)
            : System.Drawing.Icon.ExtractAssociatedIcon(iconFile);
    }
}
