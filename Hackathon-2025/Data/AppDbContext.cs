using Hackathon_2025.Models;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Story> Stories { get; set; } = null!;
    public DbSet<StoryPage> StoryPages { get; set; } = null!;

    public DbSet<StoryShare> StoryShares => Set<StoryShare>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Story>()
            .HasMany(s => s.Pages)
            .WithOne(p => p.Story)
            .HasForeignKey(p => p.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // NEW: StoryShare config
        modelBuilder.Entity<StoryShare>(e =>
        {
            e.HasIndex(s => s.Token).IsUnique();
            e.Property(s => s.Token).IsRequired().HasMaxLength(64);

            // If you added Story.Shares:
            e.HasOne(s => s.Story)
             .WithMany(st => st.Shares)
             .HasForeignKey(s => s.StoryId)
             .OnDelete(DeleteBehavior.Cascade);

            // If you did NOT add Story.Shares, use this instead:
            // e.HasOne(s => s.Story).WithMany().HasForeignKey(s => s.StoryId)
            //  .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}
