using System.Net;
using System.Net.Http.Json;
using Hackathon_2025.Models;
using Hackathon_2025.Tests.SqlServer;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hackathon_2025.Tests.Controllers;

/// <summary>
/// Story generation through the real controller. Runs on SQL Server (Testcontainers) because credit
/// reservation and refunds are atomic UPDATE statements the in-memory provider can't execute.
/// </summary>
[TestClass]
[TestCategory("SqlServer")]
public class StoryGenerationTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public async Task Init() => _factory = await SqlServerWebAppFactory.CreateAsync();

    [TestCleanup]
    public void Cleanup() => _factory?.Dispose();

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

    private Task<User> ReloadAsync(int id)
        => _factory.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id));

    private Task<int> StoryCountAsync(int userId)
        => _factory.QueryDbAsync(db => db.Stories.CountAsync(s => s.UserId == userId));

    [TestMethod]
    public async Task GenerateFull_Free_User_First_Story_Succeeds()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody());

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.AreEqual(1, (await ReloadAsync(user.Id)).BooksGenerated);
    }

    [TestMethod]
    public async Task GenerateFull_Free_User_Second_Story_Is_Forbidden()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(booksGenerated: 1));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody());

        Assert.AreEqual((HttpStatusCode)403, resp.StatusCode);
        Assert.AreEqual(0, _factory.StoryGenerator.CallCount);
        Assert.AreEqual(0, await StoryCountAsync(user.Id));
    }

    [TestMethod]
    public async Task GenerateFull_Free_User_With_AddOns_Spends_An_AddOn()
    {
        // Decided 2026-10-08: credits a Free user holds (kept after a downgrade, or the carried-over free
        // story) are spendable once the free story is used.
        var user = await _factory.SeedAsync(TestData.NewUser(booksGenerated: 1, addOnBalance: 3));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody());

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual(2, saved.AddOnBalance);
        Assert.AreEqual(1, saved.AddOnSpentThisPeriod);
        Assert.AreEqual(2, saved.BooksGenerated);
    }

    [TestMethod]
    public async Task GenerateFull_Free_User_Spending_An_AddOn_Still_Gets_Free_Plan_Limits()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(booksGenerated: 1, addOnBalance: 1));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full",
            StoryBody(fields: new Dictionary<string, string> { ["hairColor"] = "brown", ["favoriteFood"] = "pizza" }));

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var fields = _factory.StoryGenerator.LastRequest!.Characters[0].DescriptionFields;
        Assert.IsTrue(fields.ContainsKey("hairColor"));
        Assert.IsFalse(fields.ContainsKey("favoriteFood"), "paid-only character details stay locked for Free, even with a credit");
    }

    [TestMethod]
    public async Task GenerateFull_Exhausted_Pro_User_Spends_An_AddOn()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 5, addOnBalance: 2));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody());

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual(1, saved.AddOnBalance);
        Assert.AreEqual(1, saved.AddOnSpentThisPeriod);
        Assert.AreEqual(6, saved.BooksGenerated);
    }

    [TestMethod]
    public async Task GenerateFull_Exhausted_Pro_User_Without_AddOns_Is_Forbidden()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 5));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody());

        Assert.AreEqual((HttpStatusCode)403, resp.StatusCode);
    }

    [TestMethod]
    public async Task GenerateFull_Failure_Deletes_Draft_And_Refunds_Base_Credit()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        _factory.StoryGenerator.ThrowOnGenerate = new InvalidOperationException("OpenAI is down");

        await ServerErrors.AssertServerErrorAsync(() =>
            _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody()));

        Assert.AreEqual(0, await StoryCountAsync(user.Id));
        Assert.AreEqual(0, (await ReloadAsync(user.Id)).BooksGenerated);
    }

    [TestMethod]
    public async Task GenerateFull_Failure_Refunds_The_AddOn_It_Reserved()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 5, addOnBalance: 1));
        _factory.StoryGenerator.ThrowOnGenerate = new InvalidOperationException("OpenAI is down");

        await ServerErrors.AssertServerErrorAsync(() =>
            _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody()));

        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual(1, saved.AddOnBalance);
        Assert.AreEqual(0, saved.AddOnSpentThisPeriod);
        Assert.AreEqual(5, saved.BooksGenerated);
        Assert.AreEqual(0, await StoryCountAsync(user.Id));
    }

    [TestMethod]
    public async Task GenerateFull_New_Month_Rolls_Over_Before_Quota_Check()
    {
        var user = TestData.NewUser(MembershipPlan.Pro, booksGenerated: 5);
        user.LastReset = DateTime.UtcNow.AddMonths(-1);
        await _factory.SeedAsync(user);

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody());

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.AreEqual(1, (await ReloadAsync(user.Id)).BooksGenerated);
    }

    [TestMethod]
    public async Task GenerateFull_With_No_Characters_Is_Rejected_Without_Spending_Credit()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full",
            new { theme = "Space", characters = Array.Empty<object>() });

        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.AreEqual(0, (await ReloadAsync(user.Id)).BooksGenerated);
        Assert.AreEqual(0, _factory.StoryGenerator.CallCount);
    }

    [TestMethod]
    public async Task GenerateFull_Length_Hint_Disabled_Strips_Length_And_PageCount()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium));

        await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody(storyLength: "long"));

        Assert.IsNull(_factory.StoryGenerator.LastRequest!.StoryLength);
        Assert.IsNull(_factory.StoryGenerator.LastRequest!.PageCount);
    }

    [DataTestMethod]
    [DataRow(MembershipPlan.Free, "long", "short", 4)]
    [DataRow(MembershipPlan.Pro, "long", "short", 4)]
    [DataRow(MembershipPlan.Pro, "medium", "medium", 8)]
    [DataRow(MembershipPlan.Premium, "long", "long", 12)]
    public async Task GenerateFull_Length_Is_Gated_By_Plan_When_Enabled(MembershipPlan plan, string requested, string expected, int expectedPages)
    {
        using var factory = await SqlServerWebAppFactory.CreateAsync(new Dictionary<string, string?> { ["Story:LengthHintEnabled"] = "true" });
        var user = await factory.SeedAsync(TestData.NewUser(plan));

        var resp = await factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody(storyLength: requested));

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.AreEqual(expected, factory.StoryGenerator.LastRequest!.StoryLength);
        Assert.AreEqual(expectedPages, factory.StoryGenerator.LastRequest!.PageCount);
    }

    [TestMethod]
    public async Task GenerateFull_Free_Character_Fields_Are_Trimmed_And_Extra_Characters_Dropped()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var fields = new Dictionary<string, string> { ["hairColor"] = "brown", ["favoriteFood"] = "pizza" };

        await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody(fields: fields, characterCount: 2));

        var sent = _factory.StoryGenerator.LastRequest!;
        Assert.AreEqual(1, sent.Characters.Count);
        Assert.IsTrue(sent.Characters[0].DescriptionFields.ContainsKey("hairColor"));
        Assert.IsFalse(sent.Characters[0].DescriptionFields.ContainsKey("favoriteFood"));
    }

    [TestMethod]
    public async Task GenerateFull_Paid_Character_Fields_Are_Kept()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        var fields = new Dictionary<string, string> { ["hairColor"] = "brown", ["favoriteFood"] = "pizza" };

        await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody(fields: fields));

        Assert.IsTrue(_factory.StoryGenerator.LastRequest!.Characters[0].DescriptionFields.ContainsKey("favoriteFood"));
    }

    // ---------- async path: generate-full/start + result ----------

    [TestMethod]
    public async Task Start_Then_Result_Returns_The_Finished_Story()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        var client = _factory.ClientFor(user.Id);

        var start = await client.PostAsJsonAsync("/api/story/generate-full/start", StoryBody());
        Assert.AreEqual(HttpStatusCode.OK, start.StatusCode);
        var jobId = (await start.ReadJsonAsync()).GetProperty("jobId").GetString();

        string? body = null;
        await Eventually.AssertAsync(async () =>
        {
            var resp = await client.GetAsync($"/api/story/result/{jobId}");
            if (resp.StatusCode != HttpStatusCode.OK) return false;
            body = await resp.Content.ReadAsStringAsync();
            return true;
        }, "the background job should publish a result");

        StringAssert.Contains(body!, "Fake Story");
        Assert.AreEqual(1, await StoryCountAsync(user.Id));
    }

    [TestMethod]
    public async Task Start_Failure_Deletes_Draft_And_Refunds_AddOn()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 5, addOnBalance: 1));
        _factory.StoryGenerator.ThrowOnGenerate = new InvalidOperationException("OpenAI is down");

        var start = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full/start", StoryBody());
        Assert.AreEqual(HttpStatusCode.OK, start.StatusCode);

        await Eventually.AssertAsync(async () =>
        {
            var saved = await ReloadAsync(user.Id);
            return saved.AddOnBalance == 1 && saved.BooksGenerated == 5 && await StoryCountAsync(user.Id) == 0;
        }, "failed background job should refund the add-on and delete the draft");
    }

    [TestMethod]
    public async Task Start_Still_Generates_When_The_Client_Disconnects_Right_After_Being_Accepted()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        using var disconnecting = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.Configure<MvcOptions>(o => o.Filters.Add(new ClientDisconnectsAfterAcceptFilter()))));
        var client = disconnecting.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, user.Id.ToString());

        var start = await client.PostAsJsonAsync("/api/story/generate-full/start", StoryBody());

        Assert.AreEqual(HttpStatusCode.OK, start.StatusCode);
        // The credit was reserved when the request was accepted, so the story must still be made.
        await Eventually.AssertAsync(
            () => disconnecting.QueryDbAsync(db => db.Stories.Include(s => s.Pages)
                .AnyAsync(s => s.UserId == user.Id && s.Pages.Count > 0)),
            "the background job should run even though the client went away");
        Assert.AreEqual(1, _factory.StoryGenerator.CallCount);
    }
}
