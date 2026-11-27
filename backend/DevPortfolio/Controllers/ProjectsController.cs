using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DevPortfolio.Data;
using DevPortfolio.DTOs;
using DevPortfolio.Models;

namespace DevPortfolio.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProjectsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyProjects()
    {
        var userId = GetCurrentUserId();
        var projects = await _db.Projects
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new ProjectResponse(p.Id, p.Title, p.Description, p.RepoUrl, p.LiveUrl, p.TechStack, p.DisplayOrder))
            .ToListAsync();

        return Ok(projects);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProjectById(int id)
    {
        var userId = GetCurrentUserId();
        var project = await _db.Projects
            .Where(p => p.Id == id && p.UserId == userId)
            .Select(p => new ProjectResponse(p.Id, p.Title, p.Description, p.RepoUrl, p.LiveUrl, p.TechStack, p.DisplayOrder))
            .FirstOrDefaultAsync();

        if (project is null) return NotFound(new { error = "Project not found" });
        return Ok(project);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
    {
        var userId = GetCurrentUserId();
        var maxOrder = await _db.Projects
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.DisplayOrder)
            .MaxAsync() ?? -1;

        var project = new Project
        {
            UserId = userId,
            Title = request.Title.Trim(),
            Description = request.Description.NullIfBlank(),
            RepoUrl = request.RepoUrl.NullIfBlank(),
            LiveUrl = request.LiveUrl.NullIfBlank(),
            TechStack = request.TechStack.NullIfBlank(),
            DisplayOrder = maxOrder + 1,
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetProjectById), new { id = project.Id }, project);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProject(int id, [FromBody] UpdateProjectRequest request)
    {
        var userId = GetCurrentUserId();
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project is null) return NotFound(new { error = "Project not found" });

        project.Title = request.Title.Trim();
        project.Description = request.Description.NullIfBlank();
        project.RepoUrl = request.RepoUrl.NullIfBlank();
        project.LiveUrl = request.LiveUrl.NullIfBlank();
        project.TechStack = request.TechStack.NullIfBlank();
        project.DisplayOrder = request.DisplayOrder;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { message = "Project updated" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        var userId = GetCurrentUserId();
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project is null) return NotFound(new { error = "Project not found" });

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Project deleted" });
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(idClaim ?? "0");
    }
}
