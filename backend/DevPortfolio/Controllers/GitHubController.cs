using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DevPortfolio.Data;
using DevPortfolio.DTOs;
using DevPortfolio.Models;
using DevPortfolio.Services;

namespace DevPortfolio.Controllers;

[ApiController]
[Route("api/github")]
[Authorize]
public class GitHubController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly GitHubService _github;
    private readonly ActivityService _activity;

    public GitHubController(AppDbContext db, GitHubService github, ActivityService activity)
    {
        _db = db;
        _github = github;
        _activity = activity;
    }

    // The signed-in user's public, non-fork repos, flagged if already on their portfolio.
    [HttpGet("repos")]
    public async Task<ActionResult<List<GitHubRepoResponse>>> GetRepos(CancellationToken ct)
    {
        var (username, importedUrls) = await LoadUserAsync(ct);
        var repos = await _github.GetPublicReposAsync(username, ct);

        return repos
            .Select(r => new GitHubRepoResponse(
                r.Name, r.Description, r.HtmlUrl, HttpUrlAttribute.IsValidUrl(r.Homepage) ? r.Homepage.NullIfBlank() : null,
                r.Language, r.Topics ?? [], r.Stars, r.Archived, r.PushedAt,
                AlreadyImported: importedUrls.Contains(r.HtmlUrl)))
            .ToList();
    }

    // Client sends only repo names; we re-fetch the repo data from GitHub ourselves
    // so a client can't smuggle arbitrary content in under the "imported" banner.
    [HttpPost("import")]
    public async Task<ActionResult<List<ProjectResponse>>> Import([FromBody] ImportReposRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var (username, importedUrls) = await LoadUserAsync(ct);
        var wanted = request.RepoNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var repos = (await _github.GetPublicReposAsync(username, ct))
            .Where(r => wanted.Contains(r.Name) && !importedUrls.Contains(r.HtmlUrl))
            .ToList();

        var nextOrder = (await _db.Projects.Where(p => p.UserId == userId)
            .Select(p => (int?)p.DisplayOrder).MaxAsync(ct) ?? -1) + 1;

        var projects = repos.Select(r => new Project
        {
            UserId = userId,
            Title = Truncate(r.Name, 100),
            Description = r.Description is null ? null : Truncate(r.Description, 1000),
            RepoUrl = r.HtmlUrl,
            LiveUrl = HttpUrlAttribute.IsValidUrl(r.Homepage) ? r.Homepage.NullIfBlank() : null,
            TechStack = BuildTechStack(r),
            DisplayOrder = nextOrder++,
        }).ToList();

        _db.Projects.AddRange(projects);
        await _db.SaveChangesAsync(ct);
        return projects.Select(ProjectResponse.From).ToList();
    }

    [HttpPost("activity/sync")]
    public async Task<ActionResult<ActivitySummary>> SyncActivity(CancellationToken ct)
    {
        var userId = User.GetUserId();
        await _activity.SyncAsync(userId, ct);
        var syncedAt = await _db.Users.Where(u => u.Id == userId).Select(u => u.ActivitySyncedAt).FirstAsync(ct);
        return await _activity.GetSummaryAsync(userId, syncedAt, ct);
    }

    private async Task<(string Username, HashSet<string> ImportedUrls)> LoadUserAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var username = await _db.Users.Where(u => u.Id == userId).Select(u => u.Username).FirstAsync(ct);
        var urls = await _db.Projects.Where(p => p.UserId == userId && p.RepoUrl != null)
            .Select(p => p.RepoUrl!).ToListAsync(ct);
        return (username, urls.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    // Primary language first, then topics; capped to fit the 300-char column.
    internal static string? BuildTechStack(GitHubRepo repo)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(repo.Language)) parts.Add(repo.Language);
        parts.AddRange((repo.Topics ?? []).Where(t => !parts.Contains(t, StringComparer.OrdinalIgnoreCase)).Take(5));

        var result = "";
        foreach (var part in parts)
        {
            var next = result.Length == 0 ? part : $"{result},{part}";
            if (next.Length > 300) break;
            result = next;
        }
        return result.NullIfBlank();
    }

    private static string Truncate(string s, int max) => s.Length > max ? s[..max] : s;
}
