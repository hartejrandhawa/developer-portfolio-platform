using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DevPortfolio.Data;
using DevPortfolio.Models;
using DevPortfolio.Services;

namespace DevPortfolio.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    // Must NOT equal the GitHub handler's CallbackPath ("/api/auth/github/callback").
    // The handler intercepts every request to its CallbackPath, so if this action
    // lived there too, the post-login redirect would be swallowed by the handler a
    // second time and fail with "oauth state was missing or invalid".
    public const string CompleteRoute = "github/complete";

    private readonly AppDbContext _db;
    private readonly JwtService _jwtService;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext db, JwtService jwtService, IConfiguration config, ILogger<AuthController> logger)
    {
        _db = db;
        _jwtService = jwtService;
        _config = config;
        _logger = logger;
    }

    // Kicks off the GitHub OAuth flow. Frontend redirects the browser here.
    [HttpGet("github")]
    public IActionResult LoginWithGitHub()
    {
        var redirectUrl = Url.Action(nameof(CompleteGitHubLogin));
        var props = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(props, "GitHub");
    }

    // The GitHub handler finishes the OAuth exchange at its CallbackPath, signs the
    // user into the temporary cookie, then redirects here so we can issue our JWT.
    [HttpGet(CompleteRoute)]
    public async Task<IActionResult> CompleteGitHubLogin()
    {
        var frontendOrigin = _config["Frontend:Origin"] ?? "http://localhost:5173";

        var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!authResult.Succeeded || authResult.Principal is null)
        {
            return Redirect($"{frontendOrigin}/?error=login_failed");
        }

        var githubId = authResult.Principal.FindFirst("urn:github:id")?.Value;
        var githubUsername = authResult.Principal.FindFirst("urn:github:login")?.Value;
        var avatarUrl = authResult.Principal.FindFirst("urn:github:avatarurl")?.Value;
        var displayName = authResult.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

        if (githubId is null || githubUsername is null)
        {
            return Redirect($"{frontendOrigin}/?error=login_failed");
        }

        var user = await UpsertUserAsync(githubId, githubUsername, displayName, avatarUrl);
        var token = _jwtService.GenerateToken(user.Id, user.Username);

        // Clear the temporary OAuth cookie now that we've issued our own JWT
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Token goes in the URL fragment, not the query string: browsers never send
        // the fragment to any server, so it can't end up in access logs or proxies.
        var frontendUrl = _config["Frontend:CallbackUrl"] ?? $"{frontendOrigin}/auth/callback";
        return Redirect($"{frontendUrl}#token={Uri.EscapeDataString(token)}");
    }

    private async Task<User> UpsertUserAsync(string githubId, string username, string? displayName, string? avatarUrl)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.GitHubId == githubId);

        // GitHub usernames can change and be re-claimed by someone else. If a stale
        // row still holds this username, move it aside so the unique index holds;
        // that user's row gets corrected the next time they log in.
        var squatter = await _db.Users.FirstOrDefaultAsync(u => u.Username == username && u.GitHubId != githubId);
        if (squatter is not null)
        {
            _logger.LogInformation("Username {Username} moved from stale user {UserId}", username, squatter.Id);
            var renamed = $"{squatter.Username}-{squatter.GitHubId}";
            squatter.Username = renamed.Length > 50 ? renamed[..50] : renamed;
            // Saved on its own so the rename commits before the new owner takes the
            // name (SQL Server checks unique indexes per statement, and EF doesn't
            // guarantee update order within a batch).
            await _db.SaveChangesAsync();
        }

        if (user is null)
        {
            user = new User
            {
                GitHubId = githubId,
                Username = username,
                DisplayName = displayName,
                AvatarUrl = avatarUrl,
            };
            _db.Users.Add(user);
        }
        else
        {
            // Keep identity fields in sync with GitHub, but never overwrite a display
            // name the user customised in the dashboard.
            user.Username = username;
            user.AvatarUrl = avatarUrl;
            user.DisplayName ??= displayName;
        }

        await _db.SaveChangesAsync();
        return user;
    }
}
