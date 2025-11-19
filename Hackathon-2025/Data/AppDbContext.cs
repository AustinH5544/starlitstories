using Hackathon_2025.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Hackathon_2025.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Story> Stories => Set<Story>();
    public DbSet<StoryPage> StoryPages => Set<StoryPage>();
    public DbSet<ProcessedWebhook> ProcessedWebhooks => Set<ProcessedWebhook>();
    public DbSet<StoryShare> StoryShares => Set<StoryShare>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // --- Converters ---
        // Store enums as strings for readability & easy querying
        var membershipConverter = new EnumToStringConverter<MembershipPlan>();

        // ------------------ User ------------------
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);

            e.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(256);

            e.HasIndex(u => u.Email)
                .IsUnique();

            e.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(64);

            e.Property(u => u.UsernameNormalized)
                .IsRequired()
                .HasMaxLength(64);

            e.HasIndex(u => u.UsernameNormalized)
                .IsUnique();

            e.Property(u => u.PasswordHash)
                .IsRequired();

            // Membership as enum (persist as string)
            e.Property(u => u.Membership)
                .HasConversion(membershipConverter)
                .HasMaxLength(16)
                .IsRequired();

            e.Property(u => u.PlanKey)
                .IsRequired()
                .HasMaxLength(32);

            e.Property(u => u.PlanStatus)
                .IsRequired()
                .HasMaxLength(32);

            e.Property(u => u.ProfileImage)
                .HasMaxLength(512);

            e.Property(u => u.BillingProvider)
                .HasMaxLength(32);

            e.Property(u => u.BillingCustomerRef)
                .HasMaxLength(128);

            e.Property(u => u.BillingSubscriptionRef)
                .HasMaxLength(128);

            e.Property(u => u.EmailVerificationToken)
                .HasMaxLength(128);

            e.Property(u => u.PasswordResetToken)
                .HasMaxLength(128);

            // Relationships
            e.HasMany(u => u.Stories)
             .WithOne(s => s.User!)
             .HasForeignKey(s => s.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ------------------ Story ------------------
        modelBuilder.Entity<Story>(e =>
        {
            e.HasKey(s => s.Id);

            e.Property(s => s.Title)
                .IsRequired()
                .HasMaxLength(200);

            e.Property(s => s.CoverImageUrl)
                .IsRequired()
                .HasMaxLength(500);

            e.Property(s => s.CreatedAt)
                .IsRequired();

            // Story -> Pages (cascade delete)
            e.HasMany(s => s.Pages)
             .WithOne(p => p.Story)
             .HasForeignKey(p => p.StoryId)
             .OnDelete(DeleteBehavior.Cascade);

            // Story -> Shares (already configured below, but keep the inverse here)
            e.HasMany(s => s.Shares)
             .WithOne(sh => sh.Story)
             .HasForeignKey(sh => sh.StoryId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ------------------ StoryPage ------------------
        modelBuilder.Entity<StoryPage>(e =>
        {
            e.HasKey(p => p.Id);

            e.Property(p => p.Text)
                .IsRequired()
                .HasMaxLength(4000);

            e.Property(p => p.ImagePrompt)
                .IsRequired()
                .HasMaxLength(2000);

            e.Property(p => p.ImageUrl)
                .HasMaxLength(500);

            e.Property(p => p.StoryId)
                .IsRequired();
        });

        // ------------------ StoryShare ------------------
        modelBuilder.Entity<StoryShare>(e =>
        {
            e.HasKey(s => s.Id);

            e.Property(s => s.Token)
                .IsRequired()
                .HasMaxLength(64);

            e.HasIndex(s => s.Token)
                .IsUnique();

            e.Property(s => s.CreatedUtc)
                .IsRequired();

            e.HasOne(s => s.Story)
             .WithMany(st => st.Shares)
             .HasForeignKey(s => s.StoryId)
             .HasPrincipalKey(st => st.Id)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ------------------ ProcessedWebhook ------------------
        modelBuilder.Entity<ProcessedWebhook>(e =>
        {
            e.HasKey(p => p.EventId);

            e.Property(p => p.EventId)
                .IsRequired()
                .HasMaxLength(255);

            e.Property(p => p.ProcessedAtUtc)
                .IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}
