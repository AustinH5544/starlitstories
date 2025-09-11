using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Hackathon_2025.Services; // ensure this matches where IPaymentGateway is
using Checkout = Stripe.Checkout;
using BillingPortal = Stripe.BillingPortal;

using Hackathon_2025.Services;

public class StripeGateway : IPaymentGateway
{
    private readonly StripeSettings _cfg;
    private readonly AppDbContext _db;
    private readonly ILogger<StripeGateway> _log;

    public StripeGateway(IOptions<StripeSettings> cfg, AppDbContext db, ILogger<StripeGateway> log)
    {
        _cfg = cfg.Value;
        _db = db;
        StripeConfiguration.ApiKey = _cfg.SecretKey;
        _log = log;
    }

    private string MapPlanPrice(string planKey) => planKey switch
    {
        "pro" => _cfg.PriceIdPro,
        "premium" => _cfg.PriceIdPremium,
        _ => throw new ArgumentOutOfRangeException(nameof(planKey))
    };

    // ---------- Subscriptions ----------

    public async Task<CheckoutSession> CreateCheckoutSessionAsync(
        int userId, string userEmail, string planKey, string successUrl, string cancelUrl)
    {
        var lineItem = new Checkout.SessionLineItemOptions
        {
            Price = MapPlanPrice(planKey),
            Quantity = 1
        };

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
            ReturnUrl = "http://localhost:5173/profile" // TODO: move to config
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

    // ---------- One-time add-on checkout ----------

    public async Task<CheckoutSession> CreateOneTimeCheckoutAsync(
        int userId, string userEmail, string priceId, int quantity, string successUrl, string cancelUrl)
    {
        // derive a simple sku for webhook parsing
        string sku =
            priceId == _cfg.PriceIdAddon5 ? "addon_plus5" :
            priceId == _cfg.PriceIdAddon11 ? "addon_plus11" : "unknown";

        var lineItem = new Checkout.SessionLineItemOptions
        {
            Price = priceId,
            Quantity = quantity
        };

        var create = new Checkout.SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<Checkout.SessionLineItemOptions> { lineItem },
            Mode = "payment", // one-time payment
            CustomerEmail = userEmail,
            ClientReferenceId = userId.ToString(),
            Metadata = new Dictionary<string, string?>
            {
                ["userId"] = userId.ToString(),
                ["email"] = userEmail,
                ["sku"] = sku,
                ["qty"] = quantity.ToString()
            },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl
        };

        var sessSvc = new Checkout.SessionService();
        var session = await sessSvc.CreateAsync(create);
        return new CheckoutSession(session.Url);
    }

    // ---------- Webhook ----------

    public async Task<(string eventId, int? userId, string? customerRef, string? subscriptionRef,
                       string? planKey, string? status, DateTime? periodEndUtc,
                       string? addOnSku, int addOnQty)>
        HandleWebhookAsync(HttpRequest request)
    {
        var json = await new StreamReader(request.Body).ReadToEndAsync();
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, request.Headers["Stripe-Signature"], _cfg.WebhookSecret);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ConstructEvent failed");
            throw; // controller will 400/500 and log
        }

        _log.LogInformation("Stripe event received: type={Type} id={Id}", stripeEvent.Type, stripeEvent.Id);

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                {
                    var session = (Checkout.Session)stripeEvent.Data.Object;
                    var uidStr = session.ClientReferenceId ?? session.Metadata.GetValueOrDefault("userId");

                    // SUBSCRIPTION checkout
                    if (string.Equals(session.Mode, "subscription", StringComparison.OrdinalIgnoreCase))
                    {
                        return (
                            eventId: stripeEvent.Id,
                            userId: int.TryParse(uidStr, out var id) ? id : (int?)null,
                            customerRef: session.CustomerId,
                            subscriptionRef: session.SubscriptionId,
                            planKey: session.Metadata.GetValueOrDefault("plan") ?? "pro",
                            status: "active",
                            periodEndUtc: null, // optionally compute from subscription
                            addOnSku: null,
                            addOnQty: 0
                        );
                    }

                    // ONE-TIME add-on checkout
                    if (string.Equals(session.Mode, "payment", StringComparison.OrdinalIgnoreCase))
                    {
                        var sku = session.Metadata.GetValueOrDefault("sku"); // "addon_plus5" | "addon_plus11"
                        var qtyStr = session.Metadata.GetValueOrDefault("qty");
                        var qty = int.TryParse(qtyStr, out var q) ? Math.Max(1, q) : 1;

                        return (
                            eventId: stripeEvent.Id,
                            userId: int.TryParse(uidStr, out var id) ? id : (int?)null,
                            customerRef: session.CustomerId,
                            subscriptionRef: null,
                            planKey: null,
                            status: "paid",
                            periodEndUtc: null,
                            addOnSku: sku,
                            addOnQty: qty
                        );
                    }

                    // Fallback for unexpected variant
                    return (stripeEvent.Id, null, session.CustomerId, session.SubscriptionId, null, "ignored", null, null, 0);
                }

            case "customer.subscription.updated":
            case "customer.subscription.created":
                {
                    var sub = (Stripe.Subscription)stripeEvent.Data.Object;

                    // Map price -> planKey
                    var priceId = sub.Items?.Data?.FirstOrDefault()?.Price?.Id;
                    string? planKey =
                        priceId == _cfg.PriceIdPremium ? "premium" :
                        priceId == _cfg.PriceIdPro ? "pro" : null;

                    // Optionally set end from sub.CurrentPeriodEnd
                    // DateTime? end = sub.CurrentPeriodEnd.HasValue
                    //   ? DateTimeOffset.FromUnixTimeSeconds(sub.CurrentPeriodEnd.Value).UtcDateTime
                    //   : null;

                    return (
                        eventId: stripeEvent.Id,
                        userId: null, // resolve by customer/subscription in controller
                        customerRef: sub.CustomerId,
                        subscriptionRef: sub.Id,
                        planKey: planKey,
                        status: sub.Status,
                        periodEndUtc: null, // or 'end' if you compute it above
                        addOnSku: null,
                        addOnQty: 0
                    );
                }

            case "customer.subscription.deleted":
                {
                    var sub = (Stripe.Subscription)stripeEvent.Data.Object;
                    return (
                        eventId: stripeEvent.Id,
                        userId: null,
                        customerRef: sub.CustomerId,
                        subscriptionRef: sub.Id,
                        planKey: "free",
                        status: "canceled",
                        periodEndUtc: null,
                        addOnSku: null,
                        addOnQty: 0
                    );
                }

            default:
                return (stripeEvent.Id, null, null, null, null, "ignored", null, null, 0);
        }
    }
}