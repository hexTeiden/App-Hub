namespace AppHub.Models;

/// <summary>Eigene Gruppe von Apps (Side-Feature, noch ohne UI).</summary>
public class AppGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
}
