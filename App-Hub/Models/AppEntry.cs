namespace AppHub.Models;

/// <summary>Ein Programm in der Liste. Wird so wie es ist als JSON gespeichert.</summary>
public class AppEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string? Publisher { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.Now;
    public DateTime? LastLaunchedAt { get; set; }
    public bool IsFavorite { get; set; }

    /// <summary>Eigenes Bild (Cover/Logo) statt dem Icon aus der Exe.</summary>
    public string? ImagePath { get; set; }

    /// <summary>Gruppen, in denen die App ist.</summary>
    public List<Guid> GroupIds { get; set; } = [];
}
