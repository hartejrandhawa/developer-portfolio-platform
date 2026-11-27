using System.Net;
using System.Net.Http.Json;
using DevPortfolio.DTOs;
using DevPortfolio.Models;
using DevPortfolio.Tests.Infrastructure;

namespace DevPortfolio.Tests;

public class ProfileApiTests : IDisposable
{
    private readonly TestApp _app = new();
    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Unknown_profile_is_404()
    {
        var response = await _app.AnonymousClient().GetAsync("/api/profile/nobody");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Public_profile_needs_no_auth_and_lists_projects_in_display_order()
    {
        await _app.CreateUserAsync("alice", u =>
        {
            u.Bio = "Builds things";
            u.Projects.Add(new Project { Title = "Second", DisplayOrder = 1 });
            u.Projects.Add(new Project { Title = "First", DisplayOrder = 0 });
        });

        var profile = await _app.AnonymousClient().GetFromJsonAsync<ProfileResponse>("/api/profile/alice");

        Assert.Equal("alice", profile!.Username);
        Assert.Equal("Builds things", profile.Bio);
        Assert.Equal(new[] { "First", "Second" }, profile.Projects.Select(p => p.Title));
        Assert.Equal(0, profile.Activity.Total);
        Assert.Equal(ActivityServiceWindow, profile.Activity.To.DayNumber - profile.Activity.From.DayNumber + 1);
    }

    private const int ActivityServiceWindow = DevPortfolio.Services.ActivityService.WindowDays;

    [Fact]
    public async Task Me_returns_the_signed_in_user()
    {
        var client = _app.ClientFor(await _app.CreateUserAsync("alice"));
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me");
        Assert.Equal("alice", me!.Username);
        Assert.Null(me.ActivitySyncedAt);
    }

    [Fact]
    public async Task Me_with_a_token_for_a_deleted_account_is_401()
    {
        var user = await _app.CreateUserAsync("ghost");
        var client = _app.ClientFor(user);
        _app.WithDb(db => { db.Users.Remove(db.Users.Single(u => u.Id == user.Id)); return db.SaveChanges(); });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Update_profile_saves_display_name_and_bio()
    {
        var client = _app.ClientFor(await _app.CreateUserAsync("alice"));

        var response = await client.PutAsJsonAsync("/api/profile", new { displayName = " Alice A. ", bio = "Hi" });
        var me = await response.Content.ReadFromJsonAsync<MeResponse>();

        Assert.Equal("Alice A.", me!.DisplayName);
        var profile = await _app.AnonymousClient().GetFromJsonAsync<ProfileResponse>("/api/profile/alice");
        Assert.Equal("Hi", profile!.Bio);
    }

    [Fact]
    public async Task Overlong_bio_is_rejected()
    {
        var client = _app.ClientFor(await _app.CreateUserAsync("alice"));
        var response = await client.PutAsJsonAsync("/api/profile", new { displayName = "A", bio = new string('x', 501) });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
