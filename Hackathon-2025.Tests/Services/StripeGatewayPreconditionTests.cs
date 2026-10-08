using Hackathon_2025.Data;
using Hackathon_2025.Options;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Stripe;

namespace Hackathon_2025.Tests.Services;

/// <summary>
/// The real StripeGateway refuses billing-portal and cancel requests for users with nothing on file,
/// before any call to Stripe (the controller turns these into 400s; see PaymentsControllerTests).
/// </summary>
[TestClass]
public class StripeGatewayPreconditionTests
{
    private static (StripeGateway gateway, AppDbContext db) CreateGateway()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        var gateway = new StripeGateway(
            Microsoft.Extensions.Options.Options.Create(new StripeOptions { SecretKey = "sk_test_dummy" }),
            Microsoft.Extensions.Options.Options.Create(new AppOptions { BaseUrl = "https://app.test" }),
            Microsoft.Extensions.Options.Options.Create(new BillingOptions()),
            db, NullLogger<StripeGateway>.Instance, new StripeClient("sk_test_dummy"));
        return (gateway, db);
    }

    [TestMethod]
    public async Task Billing_Portal_Needs_A_Stripe_Customer()
    {
        var (gateway, db) = CreateGateway();
        var user = TestData.NewUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => gateway.CreatePortalSessionAsync(user.Id));
        StringAssert.Contains(ex.Message, "No Stripe customer");
    }

    [TestMethod]
    public async Task Cancel_Needs_An_Active_Subscription()
    {
        var (gateway, db) = CreateGateway();
        var user = TestData.NewUser();
        user.BillingCustomerRef = "cus_1"; // a customer alone isn't enough
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => gateway.CancelAtPeriodEndAsync(user.Id));
        StringAssert.Contains(ex.Message, "No active subscription");
    }
}
