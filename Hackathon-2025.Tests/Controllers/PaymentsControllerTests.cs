using System.Net;
using System.Net.Http.Json;
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Hackathon_2025.Tests.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Stripe;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class PaymentsControllerTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    // ---------- create-checkout-session ----------

    [TestMethod]
    public async Task Checkout_Requires_Login()
    {
        var resp = await _factory.AnonymousClient().PostAsJsonAsync("/api/payments/create-checkout-session", new { membership = "Pro" });
        Assert.AreEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [TestMethod]
    public async Task Checkout_For_Free_Plan_Is_Rejected()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/payments/create-checkout-session", new { membership = "Free" });
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Checkout_With_Existing_Active_Subscription_Is_Conflict()
    {
        var user = TestData.NewUser(MembershipPlan.Pro);
        user.BillingSubscriptionRef = "sub_1";
        user.PlanStatus = "active";
        await _factory.SeedAsync(user);

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/payments/create-checkout-session", new { membership = "Premium" });

        Assert.AreEqual(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [TestMethod]
    public async Task Checkout_After_Canceled_Subscription_Is_Allowed()
    {
        var user = TestData.NewUser();
        user.BillingSubscriptionRef = "sub_old";
        user.PlanStatus = "canceled";
        await _factory.SeedAsync(user);
        _factory.PaymentGatewayMock
            .Setup(g => g.CreateCheckoutSessionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CheckoutSession("https://checkout.test/s2"));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/payments/create-checkout-session", new { membership = "Pro" });

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
    }

    [TestMethod]
    public async Task Checkout_Passes_Plan_And_Return_Urls_To_Gateway()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        _factory.PaymentGatewayMock
            .Setup(g => g.CreateCheckoutSessionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CheckoutSession("https://checkout.test/s1"));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/payments/create-checkout-session", new { membership = "Pro" });

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.AreEqual("https://checkout.test/s1", (await resp.ReadJsonAsync()).GetProperty("checkoutUrl").GetString());
        _factory.PaymentGatewayMock.Verify(g => g.CreateCheckoutSessionAsync(
            user.Id, user.Email, "Pro",
            "https://app.test/profile?upgraded=1&plan=pro",
            "https://app.test/upgrade?cancelled=1"), Times.Once);
    }

    // ---------- buy-credits ----------

    [TestMethod]
    public async Task BuyCredits_Is_Premium_Only()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 5));
        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/payments/buy-credits", new { pack = "Plus5", quantity = 1 });
        Assert.AreEqual((HttpStatusCode)403, resp.StatusCode);
    }

    [TestMethod]
    public async Task BuyCredits_Requires_Base_Quota_To_Be_Used_Up()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium, booksGenerated: 3));
        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/payments/buy-credits", new { pack = "Plus5", quantity = 1 });
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task BuyCredits_Exhausted_Premium_Gets_Checkout_For_The_Right_Price()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium, booksGenerated: 11));
        _factory.PaymentGatewayMock
            .Setup(g => g.CreateOneTimeCheckoutAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CheckoutSession("https://checkout.test/credits"));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/payments/buy-credits", new { pack = "Plus11", quantity = 2 });

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        _factory.PaymentGatewayMock.Verify(g => g.CreateOneTimeCheckoutAsync(
            user.Id, user.Email, TestWebAppFactory.PriceIdAddon11, 2,
            "https://app.test/profile?credits=1", "https://app.test/profile?cancelled=1"), Times.Once);
    }

    // ---------- portal / subscription / cancel ----------

    [TestMethod]
    public async Task BillingPortal_Without_Customer_Is_BadRequest()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        _factory.PaymentGatewayMock.Setup(g => g.CreatePortalSessionAsync(user.Id))
            .ThrowsAsync(new InvalidOperationException("No Stripe customer on file."));

        var resp = await _factory.ClientFor(user.Id).GetAsync("/api/payments/billing/portal");

        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task BillingPortal_Returns_Url()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        _factory.PaymentGatewayMock.Setup(g => g.CreatePortalSessionAsync(user.Id))
            .ReturnsAsync(new PortalSession("https://portal.test/p1"));

        var resp = await _factory.ClientFor(user.Id).GetAsync("/api/payments/billing/portal");

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.AreEqual("https://portal.test/p1", (await resp.ReadJsonAsync()).GetProperty("url").GetString());
    }

    [TestMethod]
    public async Task Subscription_Returns_Stored_Billing_State()
    {
        var user = TestData.NewUser(MembershipPlan.Pro);
        user.PlanStatus = "active";
        user.BillingSubscriptionRef = "sub_9";
        user.BillingCustomerRef = "cus_9";
        await _factory.SeedAsync(user);

        var resp = await _factory.ClientFor(user.Id).GetAsync("/api/payments/subscription");

        var sub = (await resp.ReadJsonAsync()).GetProperty("subscription");
        Assert.AreEqual("active", sub.GetProperty("status").GetString());
        Assert.AreEqual("pro", sub.GetProperty("planKey").GetString());
        Assert.AreEqual("sub_9", sub.GetProperty("subscriptionRef").GetString());
        Assert.AreEqual("cus_9", sub.GetProperty("customerRef").GetString());
    }

    [TestMethod]
    public async Task Cancel_Without_Subscription_Is_BadRequest()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        _factory.PaymentGatewayMock.Setup(g => g.CancelAtPeriodEndAsync(user.Id))
            .ThrowsAsync(new InvalidOperationException("No active subscription."));

        var resp = await _factory.ClientFor(user.Id).PostAsync("/api/payments/cancel", content: null);

        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Cancel_Schedules_Cancellation_Through_Gateway()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        _factory.PaymentGatewayMock.Setup(g => g.CancelAtPeriodEndAsync(user.Id)).Returns(Task.CompletedTask);

        var resp = await _factory.ClientFor(user.Id).PostAsync("/api/payments/cancel", content: null);

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        _factory.PaymentGatewayMock.Verify(g => g.CancelAtPeriodEndAsync(user.Id), Times.Once);
    }

    // ---------- webhook paths that never reach the database ----------

    [TestMethod]
    public async Task Webhook_Bad_Signature_Returns_BadRequest()
    {
        _factory.PaymentGatewayMock.Setup(g => g.HandleWebhookAsync(It.IsAny<HttpRequest>()))
            .ThrowsAsync(new StripeException("No signatures found matching the expected signature for payload"));

        var resp = await _factory.AnonymousClient().PostAsync("/api/payments/webhook", new StringContent("{}"));

        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Webhook_Unexpected_Error_Returns_500()
    {
        _factory.PaymentGatewayMock.Setup(g => g.HandleWebhookAsync(It.IsAny<HttpRequest>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var resp = await _factory.AnonymousClient().PostAsync("/api/payments/webhook", new StringContent("{}"));

        Assert.AreEqual(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [TestMethod]
    public async Task Webhook_Non_Actionable_Event_Is_Acknowledged_Without_Recording()
    {
        _factory.PaymentGatewayMock.Setup(g => g.HandleWebhookAsync(It.IsAny<HttpRequest>()))
            .ReturnsAsync(WebhookEvents.Make("evt_ignored", status: "ignored"));

        var resp = await _factory.AnonymousClient().PostAsync("/api/payments/webhook", new StringContent("{}"));

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.AreEqual(0, await _factory.QueryDbAsync(db => db.ProcessedWebhooks.CountAsync()));
    }
}
