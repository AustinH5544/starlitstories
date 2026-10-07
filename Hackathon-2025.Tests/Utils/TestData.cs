using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Hackathon_2025.Tests.Utils;

internal static class TestData
{
    public static User NewUser(
        MembershipPlan plan = MembershipPlan.Free,
        int booksGenerated = 0,
        int addOnBalance = 0,
        string? email = null,
        string? username = null)
    {
        var name = username ?? $"u{Guid.NewGuid():N}"[..20];
        return new User
        {
            Email = email ?? $"{name}@test.local",
            Username = name,
            UsernameNormalized = name.ToLowerInvariant(),
            PasswordHash = "not-a-real-hash",
            IsEmailVerified = true,
            Membership = plan,
            PlanKey = plan.ToString().ToLowerInvariant(),
            BooksGenerated = booksGenerated,
            AddOnBalance = addOnBalance,
            LastReset = DateTime.UtcNow
        };
    }

    public static Story NewStory(int userId, int pageCount = 2, string title = "Test Story")
    {
        var story = new Story
        {
            Title = title,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            CoverImageUrl = "https://img.test/cover.png"
        };
        for (var i = 0; i < pageCount; i++)
        {
            story.Pages.Add(new StoryPage($"Page {i + 1} text", $"prompt {i + 1}") { ImageUrl = $"https://img.test/{i + 1}.png" });
        }
        return story;
    }

    public static async Task<T> SeedAsync<T>(this WebApplicationFactory<Program> factory, T entity) where T : class
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public static async Task<TResult> QueryDbAsync<TResult>(
        this WebApplicationFactory<Program> factory,
        Func<AppDbContext, Task<TResult>> query)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }
}
