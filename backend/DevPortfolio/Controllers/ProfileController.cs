using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DevPortfolio.Data;
using DevPortfolio.DTOs;
using DevPortfolio.Services;

namespace DevPortfolio.Controllers;

[ApiController]
[Route("api")]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProfileController(AppDbContext db)
    {
        _db = db;
    }

    // Public, shareable profile page — no auth required
    [HttpGet("profile/{username}")]
    public async Task<ActionResult<ProfileResponse>> GetPublicProfile(string username)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.Projects.OrderBy(p => p.DisplayOrder))
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user is null)
        {
            return NotFound(new { error = "Profile not found" });
        }

        return new ProfileResponse(
            user.Username,
            user.DisplayName,
            user.Bio,
            user.AvatarUrl,
            user.Projects.Select(ProjectResponse.From).ToList()
        );
    }

    // The signed-in user's own profile, for the dashboard header and editor.
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> GetMe()
    {
        var userId = User.GetUserId();
        var me = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new MeResponse(u.Username, u.DisplayName, u.Bio, u.AvatarUrl))
            .FirstOrDefaultAsync();

        // Valid token but the row is gone (e.g. DB reset): treat as signed out.
        if (me is null) return Unauthorized(new { error = "Account not found" });
        return me;
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<MeResponse>> UpdateOwnProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = User.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return Unauthorized(new { error = "Account not found" });

        user.DisplayName = request.DisplayName.NullIfBlank();
        user.Bio = request.Bio.NullIfBlank();
        await _db.SaveChangesAsync();

        return new MeResponse(user.Username, user.DisplayName, user.Bio, user.AvatarUrl);
    }
}
