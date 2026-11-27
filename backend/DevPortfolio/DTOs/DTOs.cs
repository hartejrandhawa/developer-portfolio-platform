using System.ComponentModel.DataAnnotations;

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

public record ProjectResponse(
    int Id,
    string Title,
    string? Description,
    string? RepoUrl,
    string? LiveUrl,
    string? TechStack,
    int DisplayOrder
);

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
    int DisplayOrder
);
