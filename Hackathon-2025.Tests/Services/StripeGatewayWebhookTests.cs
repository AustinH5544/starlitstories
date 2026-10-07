using System.Text;
using Hackathon_2025.Data;
using Hackathon_2025.Options;
using Hackathon_2025.Tests.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Stripe;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class StripeGatewayWebhookTests
{
    private const string Secret = "whsec_unit_test";

    private static StripeGateway CreateGateway()
    {
        var stripe = Microsoft.Extensions.Options.Options.Create(new StripeOptions
        {
            SecretKey = "sk_test_dummy",
            WebhookSecret = Secret,
            PriceIdPro = "price_pro",
            PriceIdPremium = "price_premium",
            PriceIdAddon5 = "price_a5",
            PriceIdAddon11 = "price_a11"
        });
        var app = Microsoft.Extensions.Options.Options.Create(new AppOptions { BaseUrl = "https://app.test" });
        var billing = Microsoft.Extensions.Options.Options.Create(new BillingOptions());
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

        return new StripeGateway(stripe, app, billing, db, NullLogger<StripeGateway>.Instance, new StripeClient("sk_test_dummy"));
    }

    private static HttpRequest SignedRequest(string payload, string? signature = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        context.Request.Headers["Stripe-Signature"] = signature ?? StripeTestEvents.Sign(payload, Secret);
        return context.Request;
    }

    [TestMethod]
    public async Task Payment_Checkout_Maps_AddOn_Sku_Quantity_User_And_Customer()
    {
        var payload = StripeTestEvents.Event("evt_pay_1", "checkout.session.completed",
            """{"id":"cs_1","object":"checkout.session","mode":"payment","client_reference_id":"42","customer":"cus_1","metadata":{"userId":"42","sku":"addon_plus5","qty":"2","priceId":"price_a5"}}""");

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload));

        Assert.AreEqual("evt_pay_1", e.eventId);
        Assert.AreEqual(42, e.userId);
        Assert.AreEqual("cus_1", e.customerRef);
        Assert.AreEqual("addon_plus5", e.addOnSku);
        Assert.AreEqual(2, e.addOnQty);
        Assert.AreEqual("paid", e.status);
        Assert.IsNull(e.planKey);
    }

    [TestMethod]
    public async Task Payment_Checkout_Without_Sku_Falls_Back_To_Price_Id()
    {
        var payload = StripeTestEvents.Event("evt_pay_2", "checkout.session.completed",
            """{"id":"cs_2","object":"checkout.session","mode":"payment","client_reference_id":"42","metadata":{"priceId":"price_a11"}}""");

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload));

        Assert.AreEqual("addon_plus11", e.addOnSku);
        Assert.AreEqual(1, e.addOnQty);
    }

    [TestMethod]
    public async Task Payment_Checkout_With_Zero_Quantity_Is_Treated_As_One()
    {
        var payload = StripeTestEvents.Event("evt_pay_3", "checkout.session.completed",
            """{"id":"cs_3","object":"checkout.session","mode":"payment","client_reference_id":"42","metadata":{"sku":"addon_plus5","qty":"0"}}""");

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload));

        Assert.AreEqual(1, e.addOnQty);
    }

    [TestMethod]
    public async Task Subscription_Checkout_Maps_Plan_From_Metadata()
    {
        var payload = StripeTestEvents.Event("evt_sub_co", "checkout.session.completed",
            """{"id":"cs_4","object":"checkout.session","mode":"subscription","client_reference_id":"7","customer":"cus_7","metadata":{"plan":"premium"}}""");

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload));

        Assert.AreEqual(7, e.userId);
        Assert.AreEqual("premium", e.planKey);
        Assert.AreEqual("active", e.status);
        Assert.AreEqual("cus_7", e.customerRef);
        Assert.IsNull(e.addOnSku);
    }

    [TestMethod]
    public async Task Subscription_Updated_Maps_Plan_From_Price_Id()
    {
        var payload = StripeTestEvents.Event("evt_sub_upd", "customer.subscription.updated",
            """{"id":"sub_1","object":"subscription","customer":"cus_1","status":"active","items":{"object":"list","data":[{"id":"si_1","object":"subscription_item","price":{"id":"price_premium","object":"price"}}]}}""");

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload));

        Assert.AreEqual("premium", e.planKey);
        Assert.AreEqual("active", e.status);
        Assert.AreEqual("cus_1", e.customerRef);
        Assert.AreEqual("sub_1", e.subscriptionRef);
        Assert.IsNull(e.userId);
    }

    [TestMethod]
    public async Task Subscription_Deleted_Maps_To_Free_And_Canceled()
    {
        var payload = StripeTestEvents.Event("evt_sub_del", "customer.subscription.deleted",
            """{"id":"sub_1","object":"subscription","customer":"cus_1","status":"canceled"}""");

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload));

        Assert.AreEqual("free", e.planKey);
        Assert.AreEqual("canceled", e.status);
        Assert.AreEqual("sub_1", e.subscriptionRef);
    }

    [TestMethod]
    public async Task Invoice_Paid_Maps_Period_Window()
    {
        var payload = StripeTestEvents.Event("evt_inv", "invoice.payment_succeeded",
            """{"id":"in_1","object":"invoice","customer":"cus_1","lines":{"object":"list","data":[{"id":"il_1","object":"line_item","period":{"start":1767225600,"end":1769904000}}]}}""");

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload));

        Assert.AreEqual("active", e.status);
        Assert.AreEqual(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), e.periodStartUtc);
        Assert.AreEqual(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), e.periodEndUtc);
    }

    [TestMethod]
    public async Task Unhandled_Event_Type_Is_Ignored()
    {
        var payload = StripeTestEvents.Event("evt_other", "charge.refunded", """{"id":"ch_1","object":"charge"}""");

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload));

        Assert.AreEqual("ignored", e.status);
        Assert.IsNull(e.planKey);
        Assert.IsNull(e.addOnSku);
    }

    [TestMethod]
    public async Task Forged_Signature_Is_Rejected()
    {
        var payload = StripeTestEvents.Event("evt_forged", "charge.refunded", """{"id":"ch_1","object":"charge"}""");
        var forged = StripeTestEvents.Sign(payload, "whsec_attacker");

        await Assert.ThrowsExceptionAsync<StripeException>(() => CreateGateway().HandleWebhookAsync(SignedRequest(payload, forged)));
    }

    [TestMethod]
    public async Task Retry_Signed_Four_Minutes_Ago_Is_Accepted()
    {
        // Regression: a tolerance of 0 seconds rejected every Stripe retry.
        var payload = StripeTestEvents.Event("evt_retry", "charge.refunded", """{"id":"ch_1","object":"charge"}""");
        var signature = StripeTestEvents.Sign(payload, Secret, DateTimeOffset.UtcNow.AddMinutes(-4));

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload, signature));

        Assert.AreEqual("evt_retry", e.eventId);
    }

    [TestMethod]
    public async Task Signature_Older_Than_Five_Minutes_Is_Rejected()
    {
        var payload = StripeTestEvents.Event("evt_stale", "charge.refunded", """{"id":"ch_1","object":"charge"}""");
        var signature = StripeTestEvents.Sign(payload, Secret, DateTimeOffset.UtcNow.AddMinutes(-10));

        await Assert.ThrowsExceptionAsync<StripeException>(() => CreateGateway().HandleWebhookAsync(SignedRequest(payload, signature)));
    }
}
