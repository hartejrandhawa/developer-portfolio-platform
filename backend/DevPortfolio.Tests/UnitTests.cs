using DevPortfolio.Controllers;
using DevPortfolio.DTOs;
using DevPortfolio.Services;

namespace DevPortfolio.Tests;

public class UnitTests
{
    private static GitHubEvent Event(string type, string repo, string at, string? action = null, int? size = null) =>
        new(type, new GitHubEventRepo(repo), new GitHubEventPayload(action, size), DateTime.Parse(at).ToUniversalTime());

    [Fact]
    public void Aggregate_groups_by_repo_type_and_day_with_push_sizes()
    {
        var rows = GitHubService.Aggregate(7, new[]
        {
            Event("PushEvent", "a/x", "2026-11-01T01:00:00Z", size: 2),
            Event("PushEvent", "a/x", "2026-11-01T23:00:00Z", size: 0), // size 0 still counts as a push
            Event("PushEvent", "a/x", "2026-11-02T01:00:00Z"),
            Event("PullRequestEvent", "a/x", "2026-11-01T05:00:00Z", action: "opened"),
            Event("PullRequestEvent", "a/x", "2026-11-01T06:00:00Z", action: "closed"),
            Event("IssuesEvent", "b/y", "2026-11-01T05:00:00Z", action: "opened"),
            Event("WatchEvent", "b/y", "2026-11-01T05:00:00Z", action: "started"),
        });

        var summary = rows
            .OrderBy(r => r.ContributionDate).ThenBy(r => r.RepoName).ThenBy(r => r.ContributionType)
            .Select(r => $"{r.ContributionDate:MM-dd} {r.RepoName} {r.ContributionType} {r.Count}");

        Assert.Equal(new[]
        {
            "11-01 a/x commit 3",
            "11-01 a/x pull_request 1",
            "11-01 b/y issue 1",
            "11-02 a/x commit 1",
        }, summary);
        Assert.All(rows, r => Assert.Equal(7, r.UserId));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("https://example.com/path?q=1", true)]
    [InlineData("http://localhost:3000", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("JaVaScRiPt:alert(1)", false)]
    [InlineData("//evil.example", false)]
    [InlineData("/relative", false)]
    [InlineData("mailto:a@b.c", false)]
    public void HttpUrl_only_allows_absolute_http_urls(string? url, bool valid) =>
        Assert.Equal(valid, HttpUrlAttribute.IsValidUrl(url));

    [Fact]
    public void Tech_stack_puts_language_first_dedupes_and_fits_the_column()
    {
        var repo = new GitHubRepo("r", null, "https://github.com/a/r", null, "Python",
            new List<string> { "python", "django", "postgres" }, false, false, 0, null);
        Assert.Equal("Python,django,postgres", GitHubController.BuildTechStack(repo));

        var huge = repo with { Language = null, Topics = Enumerable.Range(0, 5).Select(i => new string((char)('a' + i), 80)).ToList() };
        Assert.True(GitHubController.BuildTechStack(huge)!.Length <= 300);
    }
}
