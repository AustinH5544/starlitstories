using Hackathon_2025.Models;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Tests.SqlServer;

[TestClass]
[TestCategory("SqlServer")]
public class MigrationsTests
{
    [TestMethod]
    public async Task All_Migrations_Apply_To_An_Empty_Database()
    {
        using var factory = await SqlServerWebAppFactory.CreateAsync();

        var (pending, applied, known) = await factory.QueryDbAsync(async db => (
            (await db.Database.GetPendingMigrationsAsync()).ToList(),
            (await db.Database.GetAppliedMigrationsAsync()).ToList(),
            db.Database.GetMigrations().ToList()));

        Assert.AreEqual(0, pending.Count, "Pending migrations: " + string.Join(", ", pending));
        CollectionAssert.AreEquivalent(known, applied);
    }

    [TestMethod]
    public async Task Migrated_Schema_Accepts_Every_Entity_The_Model_Writes()
    {
        using var factory = await SqlServerWebAppFactory.CreateAsync();

        var user = await factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium, addOnBalance: 2));
        var story = TestData.NewStory(user.Id);
        story.RequestTheme = "Space";
        story.RequestReadingLevel = "early";
        story.RequestArtStyle = "watercolor";
        story.RequestStoryLength = "short";
        story.RequestLessonLearned = "Be kind";
        story.RequestCharactersJson = "[]";
        await factory.SeedAsync(story);
        await factory.SeedAsync(new StoryShare { StoryId = story.Id, ExpiresUtc = DateTime.UtcNow.AddDays(1) });
        await factory.SeedAsync(new SavedCharacter { UserId = user.Id, Name = "Milo", CharacterJson = "{}" });
        await factory.SeedAsync(new ProcessedWebhook { EventId = "evt_schema" });

        var counts = await factory.QueryDbAsync(async db => (
            await db.Users.CountAsync(),
            await db.Stories.CountAsync(),
            await db.StoryPages.CountAsync(),
            await db.StoryShares.CountAsync(),
            await db.SavedCharacters.CountAsync(),
            await db.ProcessedWebhooks.CountAsync()));

        Assert.AreEqual((1, 1, 2, 1, 1, 1), counts);
    }
}
