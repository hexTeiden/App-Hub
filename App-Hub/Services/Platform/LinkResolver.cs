namespace AppHub.Services.Platform;

/// <summary>Löst Symlinks auf, damit z.B. ein Link ohne Endung als das erkannt wird, worauf er zeigt.</summary>
public static class LinkResolver
{
    /// <summary>Gibt das endgültige Ziel eines Symlinks zurück, oder den Pfad selbst, wenn es kein Link ist.</summary>
    public static string Resolve(string path)
    {
        try
        {
            var target = new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true);
            return target?.FullName ?? path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return path;
        }
    }

    /// <summary>true, wenn die Datei existiert und (falls Symlink) ihr Ziel auch.</summary>
    public static bool TargetExists(string path) => File.Exists(path) && File.Exists(Resolve(path));
}
