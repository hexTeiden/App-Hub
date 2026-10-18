using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;

namespace AppHub.Services.Platform;

/// <summary>
/// Unterstützt .desktop-Dateien (z.B. aus /usr/share/applications) und normale ausführbare Dateien.
/// </summary>
public partial class LinuxPlatformService : IPlatformService
{
    public IReadOnlyList<string> ProgramFilePatterns { get; } = ["*.desktop", "*"];

    public AppInfo ReadInfo(string path)
    {
        if (IsDesktopFile(path))
        {
            var entry = ReadDesktopEntry(path);
            if (entry.TryGetValue("Name", out var name))
                return new AppInfo(name, null);
        }

        return new AppInfo(Path.GetFileNameWithoutExtension(path), null);
    }

    public Bitmap? LoadIcon(string path)
    {
        if (!IsDesktopFile(path) || !File.Exists(path))
            return null;

        // Nur absolute Icon-Pfade. TODO: Icon-Namen im Icon-Theme nachschlagen (/usr/share/icons/...)
        if (ReadDesktopEntry(path).TryGetValue("Icon", out var icon)
            && Path.IsPathRooted(icon)
            && File.Exists(icon)
            && !icon.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            try { return new Bitmap(icon); }
            catch (Exception) { return null; }
        }

        return null;
    }

    public bool Exists(string path) => File.Exists(path);

    public void Launch(string path)
    {
        if (IsDesktopFile(path))
        {
            if (!ReadDesktopEntry(path).TryGetValue("Exec", out var exec))
                throw new InvalidOperationException("Die .desktop-Datei hat keinen Exec-Eintrag.");

            // Platzhalter wie %U, %f entfernen (https://specifications.freedesktop.org/desktop-entry-spec/latest/exec-variables.html)
            var command = FieldCodeRegex().Replace(exec, "").Trim();
            Process.Start(new ProcessStartInfo("/bin/sh") { ArgumentList = { "-c", command }, UseShellExecute = false });
            return;
        }

        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(path) ?? "",
        });
    }

    private static bool IsDesktopFile(string path) =>
        path.EndsWith(".desktop", StringComparison.OrdinalIgnoreCase);

    /// <summary>Liest die Schlüssel aus dem [Desktop Entry]-Abschnitt.</summary>
    private static Dictionary<string, string> ReadDesktopEntry(string path)
    {
        var result = new Dictionary<string, string>();
        var inDesktopEntry = false;

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('['))
            {
                inDesktopEntry = line == "[Desktop Entry]";
                continue;
            }

            var separator = line.IndexOf('=');
            if (!inDesktopEntry || separator <= 0)
                continue;

            // Lokalisierte Schlüssel wie Name[de] werden ignoriert, der erste Treffer gewinnt
            result.TryAdd(line[..separator].Trim(), line[(separator + 1)..].Trim());
        }

        return result;
    }

    [GeneratedRegex("%[a-zA-Z]")]
    private static partial Regex FieldCodeRegex();
}
