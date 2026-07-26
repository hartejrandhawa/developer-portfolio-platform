namespace DevPortfolio.DTOs;

public record ProfileResponse(
    string Username,
    string? DisplayName,
    string? Bio,
    string? AvatarUrl,
    List<ProjectResponse> Projects
);

public record UpdateProfileRequest(string? DisplayName, string? Bio);

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
    string Title,
    string? Description,
    string? RepoUrl,
    string? LiveUrl,
    string? TechStack
);

public record UpdateProjectRequest(
    string Title,
    string? Description,
    string? RepoUrl,
    string? LiveUrl,
    string? TechStack,
    int DisplayOrder
);
