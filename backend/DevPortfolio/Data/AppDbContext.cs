using Microsoft.EntityFrameworkCore;
using DevPortfolio.Models;

namespace DevPortfolio.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Contribution> Contributions => Set<Contribution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.GitHubId).IsUnique();
            entity.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasOne(p => p.User)
                  .WithMany(u => u.Projects)
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Speeds up "get all projects for this user, in display order" —
            // the main query the public profile page runs
            entity.HasIndex(p => new { p.UserId, p.DisplayOrder });
        });

        modelBuilder.Entity<Contribution>(entity =>
        {
            entity.HasOne(c => c.User)
                  .WithMany(u => u.Contributions)
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(c => new { c.UserId, c.ContributionDate });
        });
    }
}
