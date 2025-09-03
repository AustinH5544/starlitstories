using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Checkout = Stripe.Checkout;
using BillingPortal = Stripe.BillingPortal;

public class StripeGateway : IPaymentGateway
{
    private readonly StripeSettings _cfg;
    private readonly AppDbContext _db;

    public StripeGateway(IOptions<StripeSettings> cfg, AppDbContext db)
    {
        _cfg = cfg.Value;
        _db = db;
        StripeConfiguration.ApiKey = _cfg.SecretKey;
    }

    private string MapPrice(string planKey) => planKey switch
    {
        "pro" => _cfg.PriceIdPro,
        "premium" => _cfg.PriceIdPremium,
        _ => throw new ArgumentOutOfRangeException(nameof(planKey))
    };

    public async Task<CheckoutSession> CreateCheckoutSessionAsync(
        int userId, string userEmail, string planKey, string successUrl, string cancelUrl)
    {
        var lineItem = new Checkout.SessionLineItemOptions { Price = MapPrice(planKey), Quantity = 1 };

        var create = new Checkout.SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<Checkout.SessionLineItemOptions> { lineItem },
            Mode = "subscription",
            CustomerEmail = userEmail,
            ClientReferenceId = userId.ToString(),
            Metadata = new Dictionary<string, string?>
            {
                ["userId"] = userId.ToString(),
                ["email"] = userEmail,
                ["plan"] = planKey
            },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl
        };

        var sessSvc = new Checkout.SessionService();
        var session = await sessSvc.CreateAsync(create);
        return new CheckoutSession(session.Url);
    }

    public async Task<PortalSession> CreatePortalSessionAsync(int userId)
    {
        var user = await _db.Users.FirstAsync(u => u.Id == userId);
        if (string.IsNullOrEmpty(user.BillingCustomerRef))
            throw new InvalidOperationException("No Stripe customer on file.");

        var svc = new BillingPortal.SessionService();
        var ps = await svc.CreateAsync(new BillingPortal.SessionCreateOptions
        {
            Customer = user.BillingCustomerRef,
            ReturnUrl = "http://localhost:5173/profile" // replace per env
        });
        return new PortalSession(ps.Url);
    }

    public async Task CancelAtPeriodEndAsync(int userId)
    {
        var user = await _db.Users.FirstAsync(u => u.Id == userId);
        if (string.IsNullOrEmpty(user.BillingSubscriptionRef))
            throw new InvalidOperationException("No active subscription.");

        var subSvc = new SubscriptionService();
        await subSvc.UpdateAsync(user.BillingSubscriptionRef,
            new SubscriptionUpdateOptions { CancelAtPeriodEnd = true });
    }

    public async Task<(int? userId, string? customerRef, string? subscriptionRef,
                   string? planKey, string status, DateTime? periodEndUtc)>
    HandleWebhookAsync(HttpRequest request)
    {
        var json = await new StreamReader(request.Body).ReadToEndAsync();
        var stripeEvent = EventUtility.ConstructEvent(
            json,
            request.Headers["Stripe-Signature"],
            _cfg.WebhookSecret
        );

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                {
                    var session = (Checkout.Session)stripeEvent.Data.Object;
                    var uid = session.ClientReferenceId ?? session.Metadata.GetValueOrDefault("userId");
                    return (
                        userId: int.TryParse(uid, out var id) ? id : (int?)null,
                        customerRef: session.CustomerId,
                        subscriptionRef: session.SubscriptionId,
                        planKey: session.Metadata.GetValueOrDefault("plan") ?? "pro",
                        status: "active",
                        periodEndUtc: null
                    );
                }

            case "customer.subscription.updated":
            case "customer.subscription.created": // in case portal creates/rewrites items
                {
                    var sub = (Stripe.Subscription)stripeEvent.Data.Object;

                    // Map current subscription item price to your internal plan key
                    var priceId = sub.Items?.Data?.FirstOrDefault()?.Price?.Id;
                    string? planKey =
                        priceId == _cfg.PriceIdPremium ? "premium" :
                        priceId == _cfg.PriceIdPro ? "pro" :
                        null;

                    // If your Stripe.NET exposes CurrentPeriodEnd (unix seconds), you can set it here:
                    // DateTime? end = sub.CurrentPeriodEnd.HasValue
                    //     ? DateTimeOffset.FromUnixTimeSeconds(sub.CurrentPeriodEnd.Value).UtcDateTime
                    //     : null;

                    return (
                        userId: null, // we'll resolve the user by customer/subscription in the controller
                        customerRef: sub.CustomerId,
                        subscriptionRef: sub.Id,
                        planKey: planKey,                 // flip plan immediately on portal change
                        status: sub.Status,               // "active", "trialing", etc.
                        periodEndUtc: null                // or `end` if available in your SDK
                    );
                }

            case "customer.subscription.deleted":
                {
                    var sub = (Stripe.Subscription)stripeEvent.Data.Object;
                    return (
                        userId: null,
                        customerRef: sub.CustomerId,
                        subscriptionRef: sub.Id,
                        planKey: "free",                  // move user to free immediately
                        status: "canceled",
                        periodEndUtc: null
                    );
                }

            default:
                return (null, null, null, null, "ignored", null);
        }
    }
}
