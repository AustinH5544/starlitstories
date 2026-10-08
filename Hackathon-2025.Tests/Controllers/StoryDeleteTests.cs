using System.Net;
using Hackathon_2025.Models;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class StoryDeleteTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    private async Task<Story> SeedStoryAsync(int userId, int pageCount = 2, DateTime? createdAt = null)
    {
        var story = TestData.NewStory(userId, pageCount);
        if (createdAt.HasValue)
            story.CreatedAt = createdAt.Value;
        return await _factory.SeedAsync(story);
    }

    [TestMethod]
    public async Task Owner_Deletes_Story_With_Its_Pages_Shares_And_Images()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var story = await SeedStoryAsync(user.Id);
        await _factory.SeedAsync(new StoryShare { StoryId = story.Id, ExpiresUtc = DateTime.UtcNow.AddDays(7) });

        var resp = await _factory.ClientFor(user.Id).DeleteAsync($"/api/story/{story.Id}");

        Assert.AreEqual(HttpStatusCode.NoContent, resp.StatusCode);
        var (stories, pages, shares) = await _factory.QueryDbAsync(async db => (
            await db.Stories.CountAsync(s => s.Id == story.Id),
            await db.StoryPages.CountAsync(),
            await db.StoryShares.CountAsync(s => s.StoryId == story.Id)));
        Assert.AreEqual((0, 0, 0), (stories, pages, shares));
        CollectionAssert.AreEquivalent(
            new[] { "https://img.test/cover.png", "https://img.test/1.png", "https://img.test/2.png" },
            _factory.BlobUploads.Deleted.ToArray());
    }

    [TestMethod]
    public async Task Other_User_Gets_NotFound_And_Story_Survives()
    {
        var owner = await _factory.SeedAsync(TestData.NewUser());
        var other = await _factory.SeedAsync(TestData.NewUser());
        var story = await SeedStoryAsync(owner.Id);

        var resp = await _factory.ClientFor(other.Id).DeleteAsync($"/api/story/{story.Id}");

        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.IsTrue(await _factory.QueryDbAsync(db => db.Stories.AnyAsync(s => s.Id == story.Id)));
        Assert.AreEqual(0, _factory.BlobUploads.Deleted.Count);
    }

    [TestMethod]
    public async Task Missing_Story_Returns_NotFound()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());

        var resp = await _factory.ClientFor(user.Id).DeleteAsync("/api/story/999999");

        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [TestMethod]
    public async Task Story_Still_Generating_Cannot_Be_Deleted()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var draft = await SeedStoryAsync(user.Id, pageCount: 0, createdAt: DateTime.UtcNow.AddMinutes(-5));

        var resp = await _factory.ClientFor(user.Id).DeleteAsync($"/api/story/{draft.Id}");

        Assert.AreEqual(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.IsTrue(await _factory.QueryDbAsync(db => db.Stories.AnyAsync(s => s.Id == draft.Id)));
    }

    // Deleting an abandoned (stuck) draft also refunds its credit, which needs SQL Server:
    // see SqlServer/StaleDraftRecoveryTests.Deleting_A_Stuck_Draft_Yourself_Also_Returns_The_Credit.

    [TestMethod]
    public async Task Image_Cleanup_Failure_Does_Not_Block_Delete()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var story = await SeedStoryAsync(user.Id);
        _factory.BlobUploads.ThrowOnDelete = new InvalidOperationException("blob storage down");

        var resp = await _factory.ClientFor(user.Id).DeleteAsync($"/api/story/{story.Id}");

        Assert.AreEqual(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.IsFalse(await _factory.QueryDbAsync(db => db.Stories.AnyAsync(s => s.Id == story.Id)));
    }
}
