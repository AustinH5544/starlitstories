using System.Net;
using System.Net.Http.Json;
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Hackathon_2025.Tests.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Hackathon_2025.Tests.SqlServer;

/// <summary>
/// Credit reservation and refunds under concurrency: simultaneous requests, and billing changes that land
/// while a story is generating. These need real SQL Server transactions, so they run against Testcontainers.
/// </summary>
[TestClass]
[TestCategory("SqlServer")]
public class CreditReservationTests
{
    private SqlServerWebAppFactory _factory = null!;

    [TestInitialize]
    public async Task Init() => _factory = await SqlServerWebAppFactory.CreateAsync();

    [TestCleanup]
    public void Cleanup() => _factory?.Dispose();

    private static object StoryBody() => new
    {
        theme = "Space Adventure",
        readingLevel = "early",
        artStyle = "watercolor",
        characters = new[]
        {
            new { name = "Milo", role = "Main", isAnimal = false, descriptionFields = new Dictionary<string, string> { ["hairColor"] = "brown" } }
        }
    };

    private int BaseQuota(MembershipPlan plan)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IQuotaService>().BaseQuotaFor(plan.ToString());
    }

    private Task<User> ReloadAsync(int id)
        => _factory.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id));

    private async Task DeliverWebhookAsync(
        (string, int?, string?, string?, string?, string?, DateTime?, DateTime?, DateTime?, string?, int) evt)
    {
        _factory.PaymentGatewayMock.Setup(g => g.HandleWebhookAsync(It.IsAny<HttpRequest>())).ReturnsAsync(evt);
        var resp = await _factory.AnonymousClient().PostAsync("/api/payments/webhook", new StringContent("{}"));
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, "webhook should apply");
    }

    /// <summary>Starts an async story that will fail, and pauses it mid-generation.</summary>
    private async Task<TaskCompletionSource> StartFailingStoryAndPauseAsync(int userId)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _factory.StoryGenerator.Hold = release;
        _factory.StoryGenerator.ThrowOnGenerate = new InvalidOperationException("generation failed (test)");

        var start = await _factory.ClientFor(userId).PostAsJsonAsync("/api/story/generate-full/start", StoryBody());
        Assert.AreEqual(HttpStatusCode.OK, start.StatusCode);
        await _factory.StoryGenerator.Started.WaitAsync(TimeSpan.FromSeconds(10));
        return release;
    }

    private Task WaitForDraftCleanupAsync(int userId) =>
        Eventually.AssertAsync(
            () => _factory.QueryDbAsync(async db => !await db.Stories.AnyAsync(s => s.UserId == userId)),
            "the failed story's draft should be removed and its credit refunded",
            timeoutMs: 15000);

    [TestMethod]
    public async Task Simultaneous_Requests_For_The_Last_Credit_Produce_One_Story()
    {
        var quota = BaseQuota(MembershipPlan.Pro);
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: quota - 1));

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody())));

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.AreEqual(1, succeeded, "only one request may spend the last credit; statuses: " +
            string.Join(", ", responses.Select(r => (int)r.StatusCode)));
        Assert.AreEqual(quota, (await ReloadAsync(user.Id)).BooksGenerated);
        Assert.AreEqual(1, await _factory.QueryDbAsync(db => db.Stories.CountAsync(s => s.UserId == user.Id)));
    }

    [TestMethod]
    public async Task Upgrade_During_A_Failed_Story_Is_Not_Reverted_By_The_Refund()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Free));
        var release = await StartFailingStoryAndPauseAsync(user.Id);

        await DeliverWebhookAsync(WebhookEvents.Make("evt_upgrade_mid_story", userId: user.Id, customerRef: "cus_1",
            subscriptionRef: "sub_1", planKey: "pro", status: "active"));
        release.SetResult();
        await WaitForDraftCleanupAsync(user.Id);

        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual(MembershipPlan.Pro, saved.Membership, "the refund must not write back the pre-upgrade plan");
        Assert.AreEqual("pro", saved.PlanKey);
        Assert.AreEqual("sub_1", saved.BillingSubscriptionRef);
        Assert.AreEqual(0, saved.BooksGenerated);
    }

    [TestMethod]
    public async Task Credits_Bought_During_A_Failed_Story_Are_Kept_And_The_Spent_Credit_Is_Returned()
    {
        var quota = BaseQuota(MembershipPlan.Premium);
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium, booksGenerated: quota, addOnBalance: 1));
        var release = await StartFailingStoryAndPauseAsync(user.Id);
        Assert.AreEqual(0, (await ReloadAsync(user.Id)).AddOnBalance, "the story should have spent the add-on credit");

        await DeliverWebhookAsync(WebhookEvents.Make("evt_buy_mid_story", userId: user.Id, addOnSku: "addon_plus5", addOnQty: 1));
        release.SetResult();
        await WaitForDraftCleanupAsync(user.Id);

        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual(6, saved.AddOnBalance, "5 bought + 1 refunded");
        Assert.AreEqual(0, saved.AddOnSpentThisPeriod);
        Assert.AreEqual(quota, saved.BooksGenerated);
    }
}
