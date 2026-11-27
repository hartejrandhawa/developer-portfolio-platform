using Microsoft.EntityFrameworkCore;
using DevPortfolio.Data;
using DevPortfolio.DTOs;

namespace DevPortfolio.Services;

/// <summary>
/// Pulls a user's recent public GitHub activity into the Contributions table and
/// summarises it for the public profile.
/// </summary>
public class ActivityService
{
    // 13 full weeks, so the heatmap is a clean 13x7 grid.
    public const int WindowDays = 91;

    // GitHub's events feed only goes back 90 days, so there's nothing to gain from
    // syncing constantly, and each sync costs up to 3 calls from a shared rate limit.
    public static readonly TimeSpan MinSyncInterval = TimeSpan.FromMinutes(10);

    private readonly AppDbContext _db;
    private readonly GitHubService _github;
    private readonly TimeProvider _clock;

    public ActivityService(AppDbContext db, GitHubService github, TimeProvider clock)
    {
        _db = db;
        _github = github;
        _clock = clock;
    }

    /// <returns>true if a sync ran, false if skipped because the last one was recent.</returns>
    public async Task<bool> SyncAsync(int userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstAsync(u => u.Id == userId, ct);
        var now = _clock.GetUtcNow().UtcDateTime;
        if (user.ActivitySyncedAt is DateTime last && now - last < MinSyncInterval)
        {
            return false;
        }

        var events = await _github.GetPublicEventsAsync(user.Username, ct);
        var rows = GitHubService.Aggregate(userId, events);

        // Replace only the window the feed actually covered. If we hit the 300-event
        // cap the feed is shorter than 90 days, and deleting older rows would throw
        // away history we can no longer re-fetch.
        var today = DateOnly.FromDateTime(now);
        var hitCap = events.Count >= GitHubService.EventPageSize * GitHubService.MaxEventPages;
        var windowStart = hitCap && rows.Count > 0
            ? rows.Min(r => r.ContributionDate)
            : today.AddDays(-90);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.Contributions
            .Where(c => c.UserId == userId && c.ContributionDate >= windowStart)
            .ExecuteDeleteAsync(ct);
        _db.Contributions.AddRange(rows.Where(r => r.ContributionDate >= windowStart));
        user.ActivitySyncedAt = now;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<ActivitySummary> GetSummaryAsync(int userId, DateTime? syncedAt, CancellationToken ct = default)
    {
        var to = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var from = to.AddDays(-(WindowDays - 1));

        // At most a few hundred rows, so aggregate in memory: simpler than
        // provider-specific GROUP BY translation and identical on SQL Server/SQLite.
        var rows = await _db.Contributions
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.ContributionDate >= from && c.ContributionDate <= to)
            .ToListAsync(ct);

        int SumOf(string type) => rows.Where(r => r.ContributionType == type).Sum(r => r.Count);

        return new ActivitySummary(
            From: from,
            To: to,
            Total: rows.Sum(r => r.Count),
            Commits: SumOf("commit"),
            PullRequests: SumOf("pull_request"),
            Issues: SumOf("issue"),
            Days: rows.GroupBy(r => r.ContributionDate)
                .OrderBy(g => g.Key)
                .Select(g => new ActivityDay(g.Key, g.Sum(r => r.Count)))
                .ToList(),
            TopRepos: rows.GroupBy(r => r.RepoName)
                .Select(g => new RepoActivity(g.Key, g.Sum(r => r.Count)))
                .OrderByDescending(r => r.Count).ThenBy(r => r.Repo)
                .Take(5)
                .ToList(),
            SyncedAt: syncedAt
        );
    }
}
