using System.Net;
using System.Net.Http.Json;
using Hackathon_2025.Models;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class StoryGenerationTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    private static object StoryBody(string? storyLength = null, Dictionary<string, string>? fields = null, int characterCount = 1)
    {
        var characters = Enumerable.Range(0, characterCount).Select(i => new
        {
            name = i == 0 ? "Milo" : $"Friend{i}",
            role = i == 0 ? "Main" : "Friend",
            isAnimal = false,
            descriptionFields = fields ?? new Dictionary<string, string> { ["hairColor"] = "brown" }
        }).ToArray();

        return new
        {
            theme = "Space Adventure",
            readingLevel = "early",
            artStyle = "watercolor",
            storyLength,
            characters
        };
    }

    [TestMethod]
    public async Task GenerateFull_Pro_User_Persists_Story_With_Blob_Urls()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody());

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var story = await _factory.QueryDbAsync(db => db.Stories.Include(s => s.Pages).SingleAsync(s => s.UserId == user.Id));
        Assert.AreEqual("Fake Story", story.Title);
        Assert.AreEqual(2, story.Pages.Count);
        Assert.IsTrue(story.CoverImageUrl!.StartsWith("https://blob.test/"), story.CoverImageUrl);
        Assert.IsTrue(story.Pages.All(p => p.ImageUrl!.StartsWith("https://blob.test/")));
        Assert.AreEqual(3, _factory.BlobUploads.Uploaded.Count, "cover + 2 pages");
        var saved = await _factory.QueryDbAsync(db => db.Users.SingleAsync(u => u.Id == user.Id));
        Assert.AreEqual(1, saved.BooksGenerated);
    }
}
