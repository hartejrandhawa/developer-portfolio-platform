using System.Net;
using System.Net.Http.Json;
using DevPortfolio.DTOs;
using DevPortfolio.Models;
using DevPortfolio.Tests.Infrastructure;

namespace DevPortfolio.Tests;

public class GitHubApiTests : IDisposable
{
    private readonly TestApp _app = new();
    public void Dispose() => _app.Dispose();

    private const string ReposJson = """
    [
      { "name": "portfolio", "description": "My site", "html_url": "https://github.com/alice/portfolio",
        "homepage": "https://alice.dev", "language": "TypeScript", "topics": ["react", "vite"],
        "fork": false, "archived": false, "stargazers_count": 12, "pushed_at": "2026-11-20T10:00:00Z" },
      { "name": "sketchy", "description": null, "html_url": "https://github.com/alice/sketchy",
        "homepage": "javascript:alert(1)", "language": null, "topics": [],
        "fork": false, "archived": true, "stargazers_count": 0, "pushed_at": null },
      { "name": "someone-elses", "description": "forked", "html_url": "https://github.com/alice/someone-elses",
        "homepage": null, "language": "Go", "topics": [],
        "fork": true, "archived": false, "stargazers_count": 0, "pushed_at": null }
    ]
    """;

    [Fact]
    public async Task Repos_excludes_forks_and_flags_already_imported()
    {
        var user = await _app.CreateUserAsync("alice", u =>
            u.Projects.Add(new Project { Title = "portfolio", RepoUrl = "https://github.com/alice/portfolio" }));
        _app.GitHub.Respond("/users/alice/repos", ReposJson);

        var repos = (await _app.ClientFor(user).GetFromJsonAsync<List<GitHubRepoResponse>>("/api/github/repos"))!;

        Assert.Equal(new[] { "portfolio", "sketchy" }, repos.Select(r => r.Name));
        Assert.True(repos[0].AlreadyImported);
        Assert.False(repos[1].AlreadyImported);
        Assert.Null(repos[1].Homepage); // unsafe homepage never reaches the client
    }

    [Fact]
    public async Task Import_creates_projects_from_github_data_and_skips_duplicates()
    {
        var user = await _app.CreateUserAsync("alice");
        var client = _app.ClientFor(user);
        _app.GitHub.Respond("/users/alice/repos", ReposJson);

        var first = await client.PostAsJsonAsync("/api/github/import", new { repoNames = new[] { "portfolio", "sketchy", "someone-elses" } });
        var imported = (await first.Content.ReadFromJsonAsync<List<ProjectResponse>>())!;

        Assert.Equal(new[] { "portfolio", "sketchy" }, imported.Select(p => p.Title)); // fork ignored
        var portfolio = imported[0];
        Assert.Equal("My site", portfolio.Description);
        Assert.Equal("https://alice.dev", portfolio.LiveUrl);
        Assert.Equal("TypeScript,react,vite", portfolio.TechStack);
        Assert.Null(imported[1].LiveUrl); // javascript: homepage dropped

        var again = await client.PostAsJsonAsync("/api/github/import", new { repoNames = new[] { "portfolio" } });
        Assert.Empty((await again.Content.ReadFromJsonAsync<List<ProjectResponse>>())!);
        Assert.Equal(2, _app.WithDb(db => db.Projects.Count(p => p.UserId == user.Id)));
    }

    [Fact]
    public async Task Import_requires_at_least_one_repo()
    {
        var client = _app.ClientFor(await _app.CreateUserAsync("alice"));
        var response = await client.PostAsJsonAsync("/api/github/import", new { repoNames = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sync_stores_activity_shows_it_on_the_profile_and_is_throttled()
    {
        var user = await _app.CreateUserAsync("alice");
        var client = _app.ClientFor(user);
        _app.GitHub.Respond("/users/alice/events/public", """
        [
          { "type": "PushEvent", "repo": { "name": "alice/portfolio" }, "payload": { "size": 3 }, "created_at": "2026-11-26T09:00:00Z" },
          { "type": "PushEvent", "repo": { "name": "alice/portfolio" }, "payload": {}, "created_at": "2026-11-26T15:00:00Z" },
          { "type": "PullRequestEvent", "repo": { "name": "org/lib" }, "payload": { "action": "opened" }, "created_at": "2026-11-25T09:00:00Z" },
          { "type": "WatchEvent", "repo": { "name": "org/lib" }, "payload": { "action": "started" }, "created_at": "2026-11-25T09:00:00Z" }
        ]
        """);

        var summary = await (await client.PostAsync("/api/github/activity/sync", null)).Content.ReadFromJsonAsync<ActivitySummary>();

        Assert.Equal(5, summary!.Total);
        Assert.Equal(4, summary.Commits);
        Assert.Equal(1, summary.PullRequests);
        Assert.Equal("alice/portfolio", summary.TopRepos[0].Repo);
        Assert.Equal(new[] { (new DateOnly(2026, 11, 25), 1), (new DateOnly(2026, 11, 26), 4) },
            summary.Days.Select(d => (d.Date, d.Count)));
        Assert.Equal(TestApp.Now.UtcDateTime, summary.SyncedAt);

        var profile = await _app.AnonymousClient().GetFromJsonAsync<ProfileResponse>("/api/profile/alice");
        Assert.Equal(5, profile!.Activity.Total);

        // A second sync inside the throttle window must not call GitHub again.
        var callsBefore = _app.GitHub.Requests.Count;
        await client.PostAsync("/api/github/activity/sync", null);
        Assert.Equal(callsBefore, _app.GitHub.Requests.Count);

        // After the window, a re-sync replaces rows rather than double-counting.
        _app.Clock.Current = TestApp.Now.AddMinutes(11);
        var resynced = await (await client.PostAsync("/api/github/activity/sync", null)).Content.ReadFromJsonAsync<ActivitySummary>();
        Assert.Equal(5, resynced!.Total);
    }

    [Fact]
    public async Task A_failed_sync_does_not_lock_the_user_out_of_retrying()
    {
        var user = await _app.CreateUserAsync("alice");
        var client = _app.ClientFor(user);
        _app.GitHub.Respond("/users/alice/events/public", """{"message":"API rate limit exceeded"}""", HttpStatusCode.Forbidden);

        var failed = await client.PostAsync("/api/github/activity/sync", null);
        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.Null(_app.WithDb(db => db.Users.Single(u => u.Id == user.Id).ActivitySyncedAt)); // claim released

        // GitHub recovers; an immediate retry must actually run, not be throttled.
        _app.GitHub.Respond("/users/alice/events/public", """
        [ { "type": "PushEvent", "repo": { "name": "alice/x" }, "payload": { "size": 2 }, "created_at": "2026-11-26T09:00:00Z" } ]
        """);
        var retried = await (await client.PostAsync("/api/github/activity/sync", null)).Content.ReadFromJsonAsync<ActivitySummary>();
        Assert.Equal(2, retried!.Total);
        Assert.Equal(TestApp.Now.UtcDateTime, retried.SyncedAt);
    }

    [Fact]
    public async Task GitHub_rate_limit_is_reported_as_502_with_a_message()
    {
        var client = _app.ClientFor(await _app.CreateUserAsync("alice"));
        _app.GitHub.Respond("/users/alice/repos", """{"message":"API rate limit exceeded"}""", HttpStatusCode.Forbidden);

        var response = await client.GetAsync("/api/github/repos");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("rate limit", await response.Content.ReadAsStringAsync());
    }
}
