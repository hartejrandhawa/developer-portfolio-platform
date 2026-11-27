using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevPortfolio.Models;

namespace DevPortfolio.Services;

public class GitHubApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public GitHubApiException(string message, HttpStatusCode? statusCode = null) : base(message) => StatusCode = statusCode;
}

// Minimal shapes of GitHub's REST responses — only the fields we use.
public record GitHubRepo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("homepage")] string? Homepage,
    [property: JsonPropertyName("language")] string? Language,
    [property: JsonPropertyName("topics")] List<string>? Topics,
    [property: JsonPropertyName("fork")] bool Fork,
    [property: JsonPropertyName("archived")] bool Archived,
    [property: JsonPropertyName("stargazers_count")] int Stars,
    [property: JsonPropertyName("pushed_at")] DateTime? PushedAt
);

public record GitHubEventRepo([property: JsonPropertyName("name")] string Name);

public record GitHubEventPayload(
    [property: JsonPropertyName("action")] string? Action,
    [property: JsonPropertyName("size")] int? Size
);

public record GitHubEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("repo")] GitHubEventRepo Repo,
    [property: JsonPropertyName("payload")] GitHubEventPayload? Payload,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt
);

/// <summary>
/// Thin typed client over GitHub's public REST API. Registered with
/// AddHttpClient so connection pooling and base headers are handled once.
/// </summary>
public class GitHubService
{
    // GitHub's public events feed only goes back 90 days / 300 events.
    public const int EventPageSize = 100;
    public const int MaxEventPages = 3;

    private readonly HttpClient _http;

    public GitHubService(HttpClient http)
    {
        _http = http;
    }

    public static void Configure(HttpClient client, IConfiguration config)
    {
        client.BaseAddress = new Uri("https://api.github.com/");
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DevPortfolio/1.0"); // GitHub rejects requests without one
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        // Unauthenticated calls share a 60 req/hour limit per server IP; a token
        // (no scopes needed for public data) raises that to 5,000.
        var token = config["GitHub:ApiToken"];
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<List<GitHubRepo>> GetPublicReposAsync(string login, CancellationToken ct = default)
    {
        var repos = await GetAsync<List<GitHubRepo>>(
            $"users/{Uri.EscapeDataString(login)}/repos?type=owner&sort=pushed&per_page=100", ct);
        return repos.Where(r => !r.Fork).ToList();
    }

    public async Task<List<GitHubEvent>> GetPublicEventsAsync(string login, CancellationToken ct = default)
    {
        var all = new List<GitHubEvent>();
        for (var page = 1; page <= MaxEventPages; page++)
        {
            var batch = await GetAsync<List<GitHubEvent>>(
                $"users/{Uri.EscapeDataString(login)}/events/public?per_page={EventPageSize}&page={page}", ct);
            all.AddRange(batch);
            if (batch.Count < EventPageSize) break;
        }
        return all;
    }

    /// <summary>
    /// Rolls raw events up into one row per (repo, type, day). Pure function so the
    /// counting rules are unit-testable without HTTP or a database.
    /// </summary>
    public static List<Contribution> Aggregate(int userId, IEnumerable<GitHubEvent> events)
    {
        return events
            .Select(e => (Event: e, Type: Classify(e)))
            .Where(x => x.Type is not null)
            .GroupBy(x => (Repo: x.Event.Repo.Name, Type: x.Type!, Date: DateOnly.FromDateTime(x.Event.CreatedAt.ToUniversalTime())))
            .Select(g => new Contribution
            {
                UserId = userId,
                RepoName = Truncate(g.Key.Repo, 150),
                ContributionType = g.Key.Type,
                ContributionDate = g.Key.Date,
                // A push may carry several commits. GitHub stopped including commit
                // lists in event payloads in 2025, so use `size` when present, else 1.
                Count = g.Sum(x => x.Type == "commit" ? Math.Max(1, x.Event.Payload?.Size ?? 1) : 1),
            })
            .ToList();
    }

    private static string? Classify(GitHubEvent e) => e.Type switch
    {
        "PushEvent" => "commit",
        "PullRequestEvent" when e.Payload?.Action == "opened" => "pull_request",
        "IssuesEvent" when e.Payload?.Action == "opened" => "issue",
        _ => null,
    };

    private static string Truncate(string s, int max) => s.Length > max ? s[..max] : s;

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(path, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new GitHubApiException("Couldn't reach GitHub. Try again in a moment.");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new GitHubApiException("GitHub user not found.", response.StatusCode);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                throw new GitHubApiException("GitHub rate limit reached. Try again later.", response.StatusCode);
            if (!response.IsSuccessStatusCode)
                throw new GitHubApiException($"GitHub returned {(int)response.StatusCode}.", response.StatusCode);

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: ct)
                ?? throw new GitHubApiException("GitHub returned an empty response.");
        }
    }
}
