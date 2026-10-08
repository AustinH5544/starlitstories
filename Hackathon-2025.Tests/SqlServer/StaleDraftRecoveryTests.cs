using System.Net;
using System.Net.Http.Json;
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hackathon_2025.Tests.SqlServer;

/// <summary>
/// Stories generate in-process; an API restart (every deploy) kills any job mid-story, leaving a page-less draft
/// and a spent credit. Recovery removes such drafts once they are clearly abandoned and returns the credit.
/// </summary>
[TestClass]
[TestCategory("SqlServer")]
public class StaleDraftRecoveryTests
{
    private SqlServerWebAppFactory _factory = null!;

    [TestInitialize]
    public async Task Init() => _factory = await SqlServerWebAppFactory.CreateAsync();

    [TestCleanup]
    public void Cleanup() => _factory?.Dispose();

    private Task<int> RecoverAsync() => _factory.Services.GetRequiredService<StaleDraftRecovery>().RecoverAsync();

    private Task<User> ReloadAsync(int id) => _factory.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id));

    private Task<bool> StoryExistsAsync(int id) => _factory.QueryDbAsync(db => db.Stories.AnyAsync(s => s.Id == id));

    private async Task<Story> SeedDraftAsync(int userId, TimeSpan age, bool reservedFromAddOn = false)
    {
        var draft = TestData.NewStory(userId, pageCount: 0);
        draft.CoverImageUrl = "/story-generating-cover.png";
        draft.CreatedAt = DateTime.UtcNow - age;
        draft.ReservedFromAddOn = reservedFromAddOn;
        return await _factory.SeedAsync(draft);
    }

    [TestMethod]
    public async Task Abandoned_Draft_That_Used_A_Plan_Credit_Is_Removed_And_Refunded()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 2, addOnBalance: 3));
        var draft = await SeedDraftAsync(user.Id, TimeSpan.FromHours(2));

        Assert.AreEqual(1, await RecoverAsync());

        Assert.IsFalse(await StoryExistsAsync(draft.Id));
        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual(1, saved.BooksGenerated);
        Assert.AreEqual(3, saved.AddOnBalance, "add-ons untouched when the plan credit was used");
    }

    [TestMethod]
    public async Task Abandoned_Draft_That_Used_An_AddOn_Gets_The_AddOn_Back()
    {
        var user = TestData.NewUser(MembershipPlan.Premium, booksGenerated: 12, addOnBalance: 0);
        user.AddOnSpentThisPeriod = 1;
        user = await _factory.SeedAsync(user);
        await SeedDraftAsync(user.Id, TimeSpan.FromHours(2), reservedFromAddOn: true);

        Assert.AreEqual(1, await RecoverAsync());

        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual(1, saved.AddOnBalance);
        Assert.AreEqual(0, saved.AddOnSpentThisPeriod);
        Assert.AreEqual(11, saved.BooksGenerated);
    }

    [TestMethod]
    public async Task Recent_Drafts_And_Finished_Stories_Are_Left_Alone()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 2));
        var generating = await SeedDraftAsync(user.Id, TimeSpan.FromMinutes(5));
        var finished = TestData.NewStory(user.Id, pageCount: 2);
        finished.CreatedAt = DateTime.UtcNow.AddDays(-3);
        finished = await _factory.SeedAsync(finished);

        Assert.AreEqual(0, await RecoverAsync());

        Assert.IsTrue(await StoryExistsAsync(generating.Id), "a story still within its generation window may be running");
        Assert.IsTrue(await StoryExistsAsync(finished.Id));
        Assert.AreEqual(2, (await ReloadAsync(user.Id)).BooksGenerated);
    }

    [TestMethod]
    public async Task Running_Recovery_Again_Does_Not_Refund_Twice()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 2));
        await SeedDraftAsync(user.Id, TimeSpan.FromHours(2));

        await RecoverAsync();
        Assert.AreEqual(0, await RecoverAsync());

        Assert.AreEqual(1, (await ReloadAsync(user.Id)).BooksGenerated);
    }

    [TestMethod]
    public async Task Deleting_A_Stuck_Draft_Yourself_Also_Returns_The_Credit()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 2));
        var draft = await SeedDraftAsync(user.Id, TimeSpan.FromHours(2));

        var resp = await _factory.ClientFor(user.Id).DeleteAsync($"/api/story/{draft.Id}");

        Assert.AreEqual(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.IsFalse(await StoryExistsAsync(draft.Id));
        Assert.AreEqual(1, (await ReloadAsync(user.Id)).BooksGenerated);
        Assert.AreEqual(0, _factory.BlobUploads.Deleted.Count, "the shared placeholder cover is never deleted");
    }

    [TestMethod]
    public async Task Starting_A_Story_Records_Which_Kind_Of_Credit_It_Used()
    {
        var quota = Scoped<IQuotaService>().BaseQuotaFor(MembershipPlan.Premium.ToString());
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium, booksGenerated: quota, addOnBalance: 1));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _factory.StoryGenerator.Hold = release;

        var start = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full/start", new
        {
            theme = "Space",
            readingLevel = "early",
            artStyle = "watercolor",
            characters = new[] { new { name = "Milo", role = "Main", isAnimal = false, descriptionFields = new Dictionary<string, string> { ["hairColor"] = "brown" } } }
        });
        Assert.AreEqual(HttpStatusCode.OK, start.StatusCode);
        await _factory.StoryGenerator.Started.WaitAsync(TimeSpan.FromSeconds(10));

        var draft = await _factory.QueryDbAsync(db => db.Stories.AsNoTracking().SingleAsync(s => s.UserId == user.Id));
        Assert.IsTrue(draft.ReservedFromAddOn, "an abandoned draft must know to give back an add-on, not a plan credit");
        release.SetResult();
    }

    private T Scoped<T>() where T : notnull => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<T>();
}
