using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using DevPortfolio.Models;

namespace DevPortfolio.DTOs;

// Max lengths mirror the column sizes in Models/ so oversized input is a clean 400
// instead of a SQL truncation error (500).

public record ProfileResponse(
    string Username,
    string? DisplayName,
    string? Bio,
    string? AvatarUrl,
    List<ProjectResponse> Projects
);

public record UpdateProfileRequest(
    [MaxLength(100)] string? DisplayName,
    [MaxLength(500)] string? Bio
);

public record MeResponse(string Username, string? DisplayName, string? Bio, string? AvatarUrl);

public record ProjectResponse(
    int Id,
    string Title,
    string? Description,
    string? RepoUrl,
    string? LiveUrl,
    string? TechStack,
    int DisplayOrder
)
{
    // One mapping, two forms: an Expression so EF can translate it into SQL
    // (selects only these columns), and a compiled delegate for in-memory entities.
    public static readonly Expression<Func<Project, ProjectResponse>> Projection =
        p => new ProjectResponse(p.Id, p.Title, p.Description, p.RepoUrl, p.LiveUrl, p.TechStack, p.DisplayOrder);

    private static readonly Func<Project, ProjectResponse> Compiled = Projection.Compile();

    public static ProjectResponse From(Project p) => Compiled(p);
}

public record CreateProjectRequest(
    [Required, MaxLength(100)] string Title,
    [MaxLength(1000)] string? Description,
    [HttpUrl, MaxLength(2048)] string? RepoUrl,
    [HttpUrl, MaxLength(2048)] string? LiveUrl,
    [MaxLength(300)] string? TechStack
);

public record UpdateProjectRequest(
    [Required, MaxLength(100)] string Title,
    [MaxLength(1000)] string? Description,
    [HttpUrl, MaxLength(2048)] string? RepoUrl,
    [HttpUrl, MaxLength(2048)] string? LiveUrl,
    [MaxLength(300)] string? TechStack,
    int? DisplayOrder = null // optional: ordering is normally changed via /reorder
);

public record ReorderRequest([Required] List<int> ProjectIds);
