using Hackathon_2025.Models;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Story> Stories => Set<Story>();
    public DbSet<StoryPage> StoryPages => Set<StoryPage>();
    public DbSet<ProcessedWebhook> ProcessedWebhooks => Set<ProcessedWebhook>();
    public DbSet<StoryShare> StoryShares => Set<StoryShare>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Story -> Pages (cascade delete)
        modelBuilder.Entity<Story>()
            .HasMany(s => s.Pages)
            .WithOne(p => p.Story)
            .HasForeignKey(p => p.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Idempotency PK (EventId)
        modelBuilder.Entity<ProcessedWebhook>()
            .HasKey(p => p.EventId);

        // StoryShare config (single, explicit relationship)
        modelBuilder.Entity<StoryShare>(e =>
        {
            e.HasKey(s => s.Id);

            e.Property(s => s.Token)
                .IsRequired()
                .HasMaxLength(64);

            e.HasIndex(s => s.Token)
                .IsUnique();

            e.HasOne(s => s.Story)
             .WithMany(st => st.Shares)   // Story has ICollection<StoryShare> Shares
             .HasForeignKey(s => s.StoryId)
             .HasPrincipalKey(st => st.Id)
             .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}