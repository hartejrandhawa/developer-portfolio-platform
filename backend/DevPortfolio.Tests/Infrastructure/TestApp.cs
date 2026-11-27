using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using DevPortfolio.Data;
using DevPortfolio.Models;
using DevPortfolio.Services;

namespace DevPortfolio.Tests.Infrastructure;

/// <summary>
/// Boots the real app in-process with three swaps: SQLite in-memory for SQL Server,
/// a fake GitHub HTTP handler, and a fixed clock. Everything else (routing, model
/// validation, the real JwtBearer pipeline) runs exactly as in production.
/// </summary>
public sealed class TestApp : WebApplicationFactory<Program>
{
    public const string JwtSecret = "integration-test-secret-that-is-long-enough-for-hs256";
    public static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    // An in-memory SQLite DB lives only as long as its connection, so hold one open.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public FakeGitHubHandler GitHub { get; } = new();
    public FixedTimeProvider Clock { get; } = new(Now);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.UseEnvironment("Testing");
        // UseSetting (not ConfigureAppConfiguration) so values are visible to code
        // in Program.cs that reads configuration eagerly at startup.
        builder.UseSetting("Jwt:Secret", JwtSecret);
        builder.UseSetting("GitHub:ClientId", "test-client-id");
        builder.UseSetting("GitHub:ClientSecret", "test-client-secret");
        builder.UseSetting("Frontend:Origin", "http://frontend.test");
        builder.UseSetting("Frontend:CallbackUrl", "http://frontend.test/auth/callback");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));

            services.AddHttpClient<GitHubService>().ConfigurePrimaryHttpMessageHandler(() => GitHub);

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return host;
    }

    public async Task<User> CreateUserAsync(string username, Action<User>? configure = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User { GitHubId = $"gh-{username}", Username = username, DisplayName = username.ToUpperInvariant() };
        configure?.Invoke(user);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>A client carrying a JWT minted by the app's own JwtService.</summary>
    public HttpClient ClientFor(User user)
    {
        using var scope = Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<JwtService>().GenerateToken(user.Id, user.Username);
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient AnonymousClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public T WithDb<T>(Func<AppDbContext, T> query)
    {
        using var scope = Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Current { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Current;
}

/// <summary>Serves canned JSON for GitHub API paths and records what was requested.</summary>
public sealed class FakeGitHubHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _responses = new();
    public List<string> Requests { get; } = new();

    public void Respond(string path, string json, HttpStatusCode status = HttpStatusCode.OK) =>
        _responses[path] = (status, json);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request.RequestUri!.PathAndQuery);
        var (status, body) = _responses.TryGetValue(request.RequestUri.AbsolutePath, out var r)
            ? r
            : (HttpStatusCode.NotFound, """{"message":"Not Found"}""");
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }
}
