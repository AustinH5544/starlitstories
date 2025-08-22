using Hackathon_2025.Models;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Story> Stories { get; set; } = null!;
    public DbSet<StoryPage> StoryPages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Story>()
            .HasMany(s => s.Pages)
            .WithOne(p => p.Story)
            .HasForeignKey(p => p.StoryId)
            .OnDelete(DeleteBehavior.Cascade); // <-- key line

        base.OnModelCreating(modelBuilder);
    }
}