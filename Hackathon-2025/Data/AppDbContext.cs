using Hackathon_2025.Models;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Story> Stories { get; set; } = null!;
    public DbSet<StoryPage> StoryPages { get; set; } = null!;

    // Idempotency table for Stripe webhooks
    public DbSet<ProcessedWebhook> ProcessedWebhooks { get; set; } = null!;

    // Story sharing table (from your partner)
    public DbSet<StoryShare> StoryShares => Set<StoryShare>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Story -> Pages cascade delete
        modelBuilder.Entity<Story>()
            .HasMany(s => s.Pages)
            .WithOne(p => p.Story)
            .HasForeignKey(p => p.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Idempotency PK (EventId)
        modelBuilder.Entity<ProcessedWebhook>()
            .HasKey(p => p.EventId);

        // StoryShare config
        modelBuilder.Entity<StoryShare>(e =>
        {
            e.HasIndex(s => s.Token).IsUnique();
            e.Property(s => s.Token).IsRequired().HasMaxLength(64);

            // Safe mapping that works even if Story.Shares nav doesn't exist
            e.HasOne(s => s.Story)
             .WithMany() // If you have Story.Shares, change to: .WithMany(st => st.Shares)
             .HasForeignKey(s => s.StoryId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}
