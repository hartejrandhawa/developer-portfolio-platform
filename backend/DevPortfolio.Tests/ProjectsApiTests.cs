using System.Net;
using System.Net.Http.Json;
using DevPortfolio.DTOs;
using DevPortfolio.Tests.Infrastructure;

namespace DevPortfolio.Tests;

public class ProjectsApiTests : IDisposable
{
    private readonly TestApp _app = new();
    public void Dispose() => _app.Dispose();

    private static object NewProject(string title = "My App", string? repoUrl = "https://github.com/a/b", string? liveUrl = null) =>
        new { title, description = "desc", repoUrl, liveUrl, techStack = "C#,React" };

    [Fact]
    public async Task Requests_without_a_token_are_rejected()
    {
        var response = await _app.AnonymousClient().GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Requests_with_a_token_signed_by_another_key_are_rejected()
    {
        var client = _app.AnonymousClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer",
            "eyJhbGciOiJIUzI1NiJ9.eyJuYW1laWQiOiIxIn0.c2lnbmF0dXJlLW5vdC12YWxpZA");
        var response = await client.GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_the_dto_and_appends_to_the_end()
    {
        var client = _app.ClientFor(await _app.CreateUserAsync("alice"));

        var first = await client.PostAsJsonAsync("/api/projects", NewProject("One"));
        var second = await client.PostAsJsonAsync("/api/projects", NewProject("Two"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var body = await second.Content.ReadAsStringAsync();
        Assert.DoesNotContain("userId", body, StringComparison.OrdinalIgnoreCase); // DTO, not the entity
        var created = await second.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.Equal("Two", created!.Title);
        Assert.Equal(1, created.DisplayOrder);
        Assert.EndsWith($"/api/projects/{created.Id}", second.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("javascript:alert(document.cookie)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://example.com/file")]
    [InlineData("not a url")]
    public async Task Non_http_urls_are_rejected(string url)
    {
        var client = _app.ClientFor(await _app.CreateUserAsync("alice"));

        var repo = await client.PostAsJsonAsync("/api/projects", NewProject(repoUrl: url));
        var live = await client.PostAsJsonAsync("/api/projects", NewProject(liveUrl: url));

        Assert.Equal(HttpStatusCode.BadRequest, repo.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, live.StatusCode);
        Assert.Equal(0, _app.WithDb(db => db.Projects.Count()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_titles_are_rejected(string title)
    {
        var client = _app.ClientFor(await _app.CreateUserAsync("alice"));
        var response = await client.PostAsJsonAsync("/api/projects", NewProject(title));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Overlong_fields_are_a_400_not_a_database_error()
    {
        var client = _app.ClientFor(await _app.CreateUserAsync("alice"));
        var response = await client.PostAsJsonAsync("/api/projects", NewProject(new string('x', 101)));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Blank_optional_fields_are_stored_as_null_and_text_is_trimmed()
    {
        var client = _app.ClientFor(await _app.CreateUserAsync("alice"));
        var response = await client.PostAsJsonAsync("/api/projects",
            new { title = "  Spaced  ", description = "   ", repoUrl = "", liveUrl = "", techStack = "" });

        var created = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.Equal("Spaced", created!.Title);
        Assert.Null(created.Description);
        Assert.Null(created.RepoUrl);
        Assert.Null(created.TechStack);
    }

    [Fact]
    public async Task Users_cannot_read_edit_or_delete_each_others_projects()
    {
        var alice = _app.ClientFor(await _app.CreateUserAsync("alice"));
        var mallory = _app.ClientFor(await _app.CreateUserAsync("mallory"));
        var created = await (await alice.PostAsJsonAsync("/api/projects", NewProject())).Content.ReadFromJsonAsync<ProjectResponse>();

        Assert.Equal(HttpStatusCode.NotFound, (await mallory.GetAsync($"/api/projects/{created!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await mallory.PutAsJsonAsync($"/api/projects/{created.Id}", NewProject("pwned"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await mallory.DeleteAsync($"/api/projects/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await mallory.PutAsJsonAsync("/api/projects/reorder", new { projectIds = new[] { created.Id } })).StatusCode);
        Assert.Empty(await mallory.GetFromJsonAsync<List<ProjectResponse>>("/api/projects") ?? []);

        var stillThere = await alice.GetFromJsonAsync<ProjectResponse>($"/api/projects/{created.Id}");
        Assert.Equal("My App", stillThere!.Title);
    }

    [Fact]
    public async Task Update_changes_fields_and_keeps_order_when_not_given()
    {
        var client = _app.ClientFor(await _app.CreateUserAsync("alice"));
        await client.PostAsJsonAsync("/api/projects", NewProject("First"));
        var second = await (await client.PostAsJsonAsync("/api/projects", NewProject("Second"))).Content.ReadFromJsonAsync<ProjectResponse>();

        var response = await client.PutAsJsonAsync($"/api/projects/{second!.Id}",
            new { title = "Renamed", description = (string?)null, repoUrl = "https://example.com", liveUrl = (string?)null, techStack = "Go" });

        var updated = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.Equal("Renamed", updated!.Title);
        Assert.Equal("Go", updated.TechStack);
        Assert.Equal(1, updated.DisplayOrder);
    }

    [Fact]
    public async Task Reorder_rewrites_display_order()
    {
        var client = _app.ClientFor(await _app.CreateUserAsync("alice"));
        var ids = new List<int>();
        foreach (var t in new[] { "A", "B", "C" })
            ids.Add((await (await client.PostAsJsonAsync("/api/projects", NewProject(t))).Content.ReadFromJsonAsync<ProjectResponse>())!.Id);

        var response = await client.PutAsJsonAsync("/api/projects/reorder", new { projectIds = new[] { ids[2], ids[0], ids[1] } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var titles = (await client.GetFromJsonAsync<List<ProjectResponse>>("/api/projects"))!.Select(p => p.Title);
        Assert.Equal(new[] { "C", "A", "B" }, titles);
    }

    [Fact]
    public async Task Reorder_rejects_partial_or_duplicate_lists()
    {
        var client = _app.ClientFor(await _app.CreateUserAsync("alice"));
        var a = (await (await client.PostAsJsonAsync("/api/projects", NewProject("A"))).Content.ReadFromJsonAsync<ProjectResponse>())!.Id;
        var b = (await (await client.PostAsJsonAsync("/api/projects", NewProject("B"))).Content.ReadFromJsonAsync<ProjectResponse>())!.Id;

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/projects/reorder", new { projectIds = new[] { a } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/projects/reorder", new { projectIds = new[] { a, a } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/projects/reorder", new { projectIds = new[] { b, a } })).StatusCode);
    }

    [Fact]
    public async Task Delete_returns_204_and_removes_the_project()
    {
        var client = _app.ClientFor(await _app.CreateUserAsync("alice"));
        var created = await (await client.PostAsJsonAsync("/api/projects", NewProject())).Content.ReadFromJsonAsync<ProjectResponse>();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/projects/{created!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/projects/{created.Id}")).StatusCode);
    }
}
