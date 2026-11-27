using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DevPortfolio.Data;
using DevPortfolio.DTOs;
using DevPortfolio.Models;
using DevPortfolio.Services;

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
    public async Task<ActionResult<List<ProjectResponse>>> GetMyProjects()
    {
        var userId = User.GetUserId();
        var projects = await _db.Projects
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.DisplayOrder)
            .Select(ProjectResponse.Projection)
            .ToListAsync();

        return projects;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectResponse>> GetProjectById(int id)
    {
        var userId = User.GetUserId();
        var project = await _db.Projects
            .Where(p => p.Id == id && p.UserId == userId)
            .Select(ProjectResponse.Projection)
            .FirstOrDefaultAsync();

        if (project is null) return NotFound(new { error = "Project not found" });
        return project;
    }

    [HttpPost]
    public async Task<ActionResult<ProjectResponse>> CreateProject([FromBody] CreateProjectRequest request)
    {
        var userId = User.GetUserId();
        var project = new Project
        {
            UserId = userId,
            Title = request.Title.Trim(),
            Description = request.Description.NullIfBlank(),
            RepoUrl = request.RepoUrl.NullIfBlank(),
            LiveUrl = request.LiveUrl.NullIfBlank(),
            TechStack = request.TechStack.NullIfBlank(),
            DisplayOrder = await NextDisplayOrderAsync(userId),
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        // Return the DTO, not the entity: the entity exposes UserId and navigation
        // properties that aren't part of the API contract.
        return CreatedAtAction(nameof(GetProjectById), new { id = project.Id }, ProjectResponse.From(project));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProjectResponse>> UpdateProject(int id, [FromBody] UpdateProjectRequest request)
    {
        var userId = User.GetUserId();
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project is null) return NotFound(new { error = "Project not found" });

        project.Title = request.Title.Trim();
        project.Description = request.Description.NullIfBlank();
        project.RepoUrl = request.RepoUrl.NullIfBlank();
        project.LiveUrl = request.LiveUrl.NullIfBlank();
        project.TechStack = request.TechStack.NullIfBlank();
        if (request.DisplayOrder is int order) project.DisplayOrder = order;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ProjectResponse.From(project);
    }

    // Takes the full ordered list of the user's project ids and rewrites
    // DisplayOrder to match. Sending the whole list (instead of "move X up")
    // keeps the server stateless and makes the final order unambiguous.
    [HttpPut("reorder")]
    public async Task<ActionResult<List<ProjectResponse>>> Reorder([FromBody] ReorderRequest request)
    {
        var userId = User.GetUserId();
        var projects = await _db.Projects.Where(p => p.UserId == userId).ToListAsync();

        var ownedIds = projects.Select(p => p.Id).ToHashSet();
        if (request.ProjectIds.Count != ownedIds.Count
            || request.ProjectIds.Distinct().Count() != request.ProjectIds.Count
            || !request.ProjectIds.All(ownedIds.Contains))
        {
            return BadRequest(new { error = "projectIds must list each of your projects exactly once" });
        }

        var byId = projects.ToDictionary(p => p.Id);
        for (var i = 0; i < request.ProjectIds.Count; i++)
        {
            byId[request.ProjectIds[i]].DisplayOrder = i;
        }
        await _db.SaveChangesAsync();

        return request.ProjectIds.Select(id => ProjectResponse.From(byId[id])).ToList();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        var userId = User.GetUserId();
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project is null) return NotFound(new { error = "Project not found" });

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<int> NextDisplayOrderAsync(int userId)
    {
        var maxOrder = await _db.Projects
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.DisplayOrder)
            .MaxAsync();
        return (maxOrder ?? -1) + 1;
    }
}
