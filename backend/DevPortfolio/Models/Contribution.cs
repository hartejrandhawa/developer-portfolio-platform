using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevPortfolio.Models;

public class Contribution
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required]
    [MaxLength(150)]
    public string RepoName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ContributionType { get; set; } = "commit"; // commit, pull_request, issue

    public int Count { get; set; }

    public DateOnly ContributionDate { get; set; }
}
