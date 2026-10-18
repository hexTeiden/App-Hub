namespace AppHub.Models;

/// <summary>
/// Eigene Gruppe von Apps. Welche Apps drin sind, steht in <see cref="AppEntry.GroupIds"/>,
/// damit die Apps in data.json nicht doppelt gespeichert werden.
/// </summary>
public class AppGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
}
