using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DevPortfolio.Data;
using DevPortfolio.DTOs;

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
    public async Task<IActionResult> GetPublicProfile(string username)
    {
        var user = await _db.Users
            .Include(u => u.Projects.OrderBy(p => p.DisplayOrder))
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user is null)
        {
            return NotFound(new { error = "Profile not found" });
        }

        var response = new ProfileResponse(
            user.Username,
            user.DisplayName,
            user.Bio,
            user.AvatarUrl,
            user.Projects.Select(p => new ProjectResponse(
                p.Id, p.Title, p.Description, p.RepoUrl, p.LiveUrl, p.TechStack, p.DisplayOrder
            )).ToList()
        );

        return Ok(response);
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateOwnProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = GetCurrentUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        user.DisplayName = request.DisplayName.NullIfBlank();
        user.Bio = request.Bio.NullIfBlank();
        await _db.SaveChangesAsync();

        return Ok(new { message = "Profile updated" });
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(idClaim ?? "0");
    }
}
