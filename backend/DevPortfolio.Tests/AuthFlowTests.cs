using System.Net;
using AspNet.Security.OAuth.GitHub;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using DevPortfolio.Controllers;
using DevPortfolio.Tests.Infrastructure;

namespace DevPortfolio.Tests;

public class AuthFlowTests : IDisposable
{
    private readonly TestApp _app = new();
    public void Dispose() => _app.Dispose();

    [Fact]
    public void Completion_route_is_not_the_oauth_handlers_callback_path()
    {
        // Regression: when these matched, the handler re-intercepted its own
        // post-login redirect and every login failed with "oauth state was missing".
        var options = _app.Services.GetRequiredService<IOptionsMonitor<GitHubAuthenticationOptions>>().Get("GitHub");
        Assert.Equal("/api/auth/github/callback", options.CallbackPath.Value);
        Assert.NotEqual($"/api/auth/{AuthController.CompleteRoute}", options.CallbackPath.Value);
    }

    [Fact]
    public async Task Login_redirects_to_github_with_the_registered_callback()
    {
        var response = await _app.AnonymousClient().GetAsync("/api/auth/github");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith("https://github.com/login/oauth/authorize", location);
        Assert.Contains(Uri.EscapeDataString("/api/auth/github/callback"), location);
        Assert.Contains("client_id=test-client-id", location);
    }

    [Fact]
    public async Task Completing_without_an_oauth_session_sends_the_user_back_with_an_error()
    {
        var response = await _app.AnonymousClient().GetAsync($"/api/auth/{AuthController.CompleteRoute}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("http://frontend.test/?error=login_failed", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Denying_consent_on_github_sends_the_user_back_instead_of_erroring()
    {
        var response = await _app.AnonymousClient().GetAsync("/api/auth/github/callback?error=access_denied&state=x");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("http://frontend.test/?error=", response.Headers.Location!.ToString());
    }
}
