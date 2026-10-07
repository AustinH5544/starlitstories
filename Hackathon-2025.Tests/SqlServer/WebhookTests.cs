using System.Net;
using Hackathon_2025.Models;
using Hackathon_2025.Tests.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Hackathon_2025.Tests.SqlServer;

[TestClass]
[TestCategory("SqlServer")]
public class WebhookTests
{
    private static readonly DateTime PeriodStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    private SqlServerWebAppFactory _factory = null!;

    [TestInitialize]
    public async Task Init() => _factory = await SqlServerWebAppFactory.CreateAsync();

    [TestCleanup]
    public void Cleanup() => _factory?.Dispose();

    private void GatewayReturns(
        (string, int?, string?, string?, string?, string?, DateTime?, DateTime?, DateTime?, string?, int) evt)
        => _factory.PaymentGatewayMock.Setup(g => g.HandleWebhookAsync(It.IsAny<HttpRequest>())).ReturnsAsync(evt);

    private Task<HttpResponseMessage> PostWebhookAsync()
        => _factory.AnonymousClient().PostAsync("/api/payments/webhook", new StringContent("{}"));

    private Task<User> ReloadAsync(int id)
        => _factory.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id));

    [TestMethod]
    public async Task Upgrade_Free_To_Pro_Sets_Plan_Status_Period_And_Billing_Refs()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        GatewayReturns(WebhookEvents.Make("evt_up_pro", userId: user.Id, customerRef: "cus_1", subscriptionRef: "sub_1",
            planKey: "pro", status: "active", periodEndUtc: PeriodEnd, periodStartUtc: PeriodStart));

        Assert.AreEqual(HttpStatusCode.OK, (await PostWebhookAsync()).StatusCode);

        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual(MembershipPlan.Pro, saved.Membership);
        Assert.AreEqual("pro", saved.PlanKey);
        Assert.AreEqual("active", saved.PlanStatus);
        Assert.AreEqual("stripe", saved.BillingProvider);
        Assert.AreEqual("cus_1", saved.BillingCustomerRef);
        Assert.AreEqual("sub_1", saved.BillingSubscriptionRef);
        Assert.AreEqual(PeriodStart, saved.CurrentPeriodStartUtc);
        Assert.AreEqual(PeriodEnd, saved.CurrentPeriodEndUtc);
    }

    [TestMethod]
    public async Task Upgrade_With_Unused_Free_Story_Carries_It_Over_As_Credit()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(booksGenerated: 0));
        GatewayReturns(WebhookEvents.Make("evt_carry", userId: user.Id, planKey: "pro", status: "active"));

        await PostWebhookAsync();

        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual(1, saved.AddOnBalance);
        Assert.AreEqual(0, saved.BooksGenerated);
    }

    [TestMethod]
    public async Task Upgrade_After_Using_Free_Story_Gives_No_Carryover_And_Resets_Counters()
    {
        var user = TestData.NewUser(booksGenerated: 1);
        user.AddOnSpentThisPeriod = 1;
        await _factory.SeedAsync(user);
        GatewayReturns(WebhookEvents.Make("evt_nocarry", userId: user.Id, planKey: "premium", status: "active"));

        await PostWebhookAsync();

        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual(MembershipPlan.Premium, saved.Membership);
        Assert.AreEqual(0, saved.AddOnBalance);
        Assert.AreEqual(0, saved.BooksGenerated);
        Assert.AreEqual(0, saved.AddOnSpentThisPeriod);
    }

    [TestMethod]
    public async Task Unknown_Plan_Key_Is_Stored_But_Membership_Unchanged()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        GatewayReturns(WebhookEvents.Make("evt_gold", userId: user.Id, planKey: "gold", status: "active"));

        await PostWebhookAsync();

        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual("gold", saved.PlanKey);
        Assert.AreEqual(MembershipPlan.Pro, saved.Membership);
    }

    [DataTestMethod]
    [DataRow("addon_plus5", 3, 15)]
    [DataRow("addon_plus11", 2, 22)]
    [DataRow("addon_unknown", 4, 0)]
    public async Task AddOn_Purchase_Credits_Pack_Size_Times_Quantity(string sku, int qty, int expectedCredits)
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium, addOnBalance: 1));
        GatewayReturns(WebhookEvents.Make($"evt_{sku}_{qty}", userId: user.Id, status: "paid", addOnSku: sku, addOnQty: qty));

        Assert.AreEqual(HttpStatusCode.OK, (await PostWebhookAsync()).StatusCode);

        Assert.AreEqual(1 + expectedCredits, (await ReloadAsync(user.Id)).AddOnBalance);
    }

    [TestMethod]
    public async Task User_Is_Found_By_Customer_Ref_When_No_User_Id()
    {
        var user = TestData.NewUser(MembershipPlan.Pro);
        user.BillingCustomerRef = "cus_lookup";
        await _factory.SeedAsync(user);
        GatewayReturns(WebhookEvents.Make("evt_by_cus", customerRef: "cus_lookup", status: "active", periodEndUtc: PeriodEnd));

        await PostWebhookAsync();

        Assert.AreEqual(PeriodEnd, (await ReloadAsync(user.Id)).CurrentPeriodEndUtc);
    }

    [TestMethod]
    public async Task User_Is_Found_By_Subscription_Ref_When_No_User_Or_Customer()
    {
        var user = TestData.NewUser(MembershipPlan.Pro);
        user.BillingSubscriptionRef = "sub_lookup";
        await _factory.SeedAsync(user);
        GatewayReturns(WebhookEvents.Make("evt_by_sub", subscriptionRef: "sub_lookup", planKey: "premium", status: "active"));

        await PostWebhookAsync();

        Assert.AreEqual(MembershipPlan.Premium, (await ReloadAsync(user.Id)).Membership);
    }

    [TestMethod]
    public async Task Unknown_User_Is_Acknowledged_And_Event_Is_Consumed()
    {
        GatewayReturns(WebhookEvents.Make("evt_nobody", userId: 999_999, planKey: "pro", status: "active"));

        Assert.AreEqual(HttpStatusCode.OK, (await PostWebhookAsync()).StatusCode);

        Assert.AreEqual(1, await _factory.QueryDbAsync(db => db.ProcessedWebhooks.CountAsync(w => w.EventId == "evt_nobody")));
    }

    [TestMethod]
    public async Task Cancellation_Records_Status_And_Cancel_Date()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        var cancelAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        GatewayReturns(WebhookEvents.Make("evt_cancel", userId: user.Id, status: "canceled", cancelAtUtc: cancelAt));

        await PostWebhookAsync();

        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual("canceled", saved.PlanStatus);
        Assert.AreEqual(cancelAt, saved.CancelAtUtc);
    }

    [TestMethod]
    public async Task Downgrade_To_Free_Keeps_Purchased_AddOn_Balance()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium, addOnBalance: 7));
        GatewayReturns(WebhookEvents.Make("evt_downgrade", userId: user.Id, planKey: "free", status: "canceled"));

        await PostWebhookAsync();

        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual(MembershipPlan.Free, saved.Membership);
        Assert.AreEqual(7, saved.AddOnBalance);
    }

    [TestMethod]
    public async Task Same_Event_Delivered_Twice_Is_Applied_Once()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium));
        GatewayReturns(WebhookEvents.Make("evt_dupe", userId: user.Id, status: "paid", addOnSku: "addon_plus5", addOnQty: 1));

        Assert.AreEqual(HttpStatusCode.OK, (await PostWebhookAsync()).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await PostWebhookAsync()).StatusCode);

        Assert.AreEqual(5, (await ReloadAsync(user.Id)).AddOnBalance);
    }

    [TestMethod]
    public async Task Same_Event_Delivered_Concurrently_Is_Applied_Once()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium));
        GatewayReturns(WebhookEvents.Make("evt_race", userId: user.Id, status: "paid", addOnSku: "addon_plus5", addOnQty: 1));

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => PostWebhookAsync()));

        Assert.IsTrue(responses.All(r => r.StatusCode == HttpStatusCode.OK),
            "statuses: " + string.Join(",", responses.Select(r => (int)r.StatusCode)));
        Assert.AreEqual(5, (await ReloadAsync(user.Id)).AddOnBalance);
        Assert.AreEqual(1, await _factory.QueryDbAsync(db => db.ProcessedWebhooks.CountAsync(w => w.EventId == "evt_race")));
    }
}
