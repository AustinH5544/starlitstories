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

    // NEW: idempotency table for Stripe webhooks
    public DbSet<ProcessedWebhook> ProcessedWebhooks { get; set; } = null!; // NEW

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Story>()
            .HasMany(s => s.Pages)
            .WithOne(p => p.Story)
            .HasForeignKey(p => p.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // NEW: make EventId the primary key so duplicates throw and we can ignore retries
        modelBuilder.Entity<ProcessedWebhook>()
            .HasKey(p => p.EventId); // NEW

        base.OnModelCreating(modelBuilder);
    }
}