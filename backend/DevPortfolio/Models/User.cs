using System.ComponentModel.DataAnnotations;

namespace DevPortfolio.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string GitHubId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? DisplayName { get; set; }

    [MaxLength(500)]
    public string? Bio { get; set; }

    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // When GitHub activity was last pulled into Contributions; null = never.
    public DateTime? ActivitySyncedAt { get; set; }

    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<Contribution> Contributions { get; set; } = new List<Contribution>();
}
