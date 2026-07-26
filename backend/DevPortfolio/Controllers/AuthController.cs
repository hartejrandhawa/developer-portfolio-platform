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
    private readonly AppDbContext _db;
    private readonly JwtService _jwtService;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, JwtService jwtService, IConfiguration config)
    {
        _db = db;
        _jwtService = jwtService;
        _config = config;
    }

    // Kicks off the GitHub OAuth flow. Frontend redirects the browser here.
    [HttpGet("github")]
    public IActionResult LoginWithGitHub()
    {
        var redirectUrl = Url.Action(nameof(GitHubCallback));
        var props = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(props, "GitHub");
    }

    // GitHub redirects back here after the user authorizes the app.
    [HttpGet("github/callback")]
    public async Task<IActionResult> GitHubCallback()
    {
        var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!authResult.Succeeded || authResult.Principal is null)
        {
            return Unauthorized(new { error = "GitHub authentication failed" });
        }

        var githubId = authResult.Principal.FindFirst("urn:github:id")?.Value;
        var githubUsername = authResult.Principal.FindFirst("urn:github:login")?.Value;
        var avatarUrl = authResult.Principal.FindFirst("urn:github:avatarurl")?.Value;
        var displayName = authResult.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

        if (githubId is null || githubUsername is null)
        {
            return Unauthorized(new { error = "Could not read GitHub profile" });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.GitHubId == githubId);
        if (user is null)
        {
            user = new User
            {
                GitHubId = githubId,
                Username = githubUsername,
                DisplayName = displayName,
                AvatarUrl = avatarUrl,
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        var token = _jwtService.GenerateToken(user.Id, user.Username);

        // Clear the temporary OAuth cookie now that we've issued our own JWT
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var frontendUrl = _config["Frontend:CallbackUrl"] ?? "http://localhost:5173/auth/callback";
        return Redirect($"{frontendUrl}?token={token}");
    }
}
