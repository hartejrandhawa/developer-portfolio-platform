using System.Net;
using DevPortfolio.Tests.Infrastructure;

namespace DevPortfolio.Tests;

public class HostingTests : IDisposable
{
    private readonly TestApp _app = new();
    public void Dispose() => _app.Dispose();

    [Theory]
    [InlineData("/api/does-not-exist")]
    [InlineData("/api/projects/not-a-number")]
    public async Task Unknown_api_paths_are_404_not_the_spa_shell(string path)
    {
        // The SPA fallback must never swallow API paths, or clients would get
        // index.html with a 200 instead of a real error.
        var response = await _app.AnonymousClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("text/html", response.Content.Headers.ContentType?.MediaType ?? "");
    }
}
