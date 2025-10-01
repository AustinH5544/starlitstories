using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Hackathon_2025.Options;
using Hackathon_2025.Services; // IPaymentGateway
using Checkout = Stripe.Checkout;
using BillingPortal = Stripe.BillingPortal;

public class StripeGateway : IPaymentGateway
{
    private readonly StripeOptions _cfg;
    private readonly AppOptions _app;                 // NEW
    private readonly AppDbContext _db;
    private readonly ILogger<StripeGateway> _log;
    private readonly StripeClient _client;

    public StripeGateway(
        IOptions<StripeOptions> cfg,
        IOptions<AppOptions> app,                    // NEW
        AppDbContext db,
        ILogger<StripeGateway> log,
        StripeClient client)
    {
        _cfg = cfg.Value;
        _app = app.Value;                           // NEW
        _db = db;
        _log = log;
        _client = client;                           // use this instead of StripeConfiguration.ApiKey
    }

    private string MapPlanPrice(string planKey)
    {
        var key = (planKey ?? string.Empty).Trim().ToLowerInvariant(); // NEW normalize
        return key switch
        {
            "pro" => _cfg.PriceIdPro,
            "premium" => _cfg.PriceIdPremium,
            _ => throw new ArgumentOutOfRangeException(nameof(planKey), $"Unknown plan '{planKey}'")
        };
    }

    private bool IsAllowedAddon(string priceId) =>
        priceId == _cfg.PriceIdAddon5 || priceId == _cfg.PriceIdAddon11;

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

        var sessSvc = new Checkout.SessionService(_client);      // CHANGED: pass client
        var session = await sessSvc.CreateAsync(create);
        return new CheckoutSession(session.Url);
    }

    public async Task<PortalSession> CreatePortalSessionAsync(int userId)
    {
        var user = await _db.Users.FirstAsync(u => u.Id == userId);
        if (string.IsNullOrEmpty(user.BillingCustomerRef))
            throw new InvalidOperationException("No Stripe customer on file.");

        var svc = new BillingPortal.SessionService(_client);     // CHANGED: pass client
        var ps = await svc.CreateAsync(new BillingPortal.SessionCreateOptions
        {
            Customer = user.BillingCustomerRef,
            ReturnUrl = $"{_app.BaseUrl.TrimEnd('/')}/profile"  // CHANGED: from AppOptions
        });
        return new PortalSession(ps.Url);
    }

    public async Task CancelAtPeriodEndAsync(int userId)
    {
        var user = await _db.Users.FirstAsync(u => u.Id == userId);
        if (string.IsNullOrEmpty(user.BillingSubscriptionRef))
            throw new InvalidOperationException("No active subscription.");

        var subSvc = new SubscriptionService(_client);           // CHANGED: pass client
        await subSvc.UpdateAsync(user.BillingSubscriptionRef,
            new SubscriptionUpdateOptions { CancelAtPeriodEnd = true });
    }

    // ---------- One-time add-on checkout ----------

    public async Task<CheckoutSession> CreateOneTimeCheckoutAsync(
        int userId, string userEmail, string priceId, int quantity, string successUrl, string cancelUrl)
    {
        if (!IsAllowedAddon(priceId))                            // NEW: guard
            throw new ArgumentException("Unknown add-on priceId.", nameof(priceId));

        string sku = priceId == _cfg.PriceIdAddon5 ? "addon_plus5" : "addon_plus11";

        var lineItem = new Checkout.SessionLineItemOptions
        {
            Price = priceId,
            Quantity = Math.Max(1, quantity)                     // small safety
        };

        var create = new Checkout.SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<Checkout.SessionLineItemOptions> { lineItem },
            Mode = "payment",
            CustomerEmail = userEmail,
            ClientReferenceId = userId.ToString(),
            Metadata = new Dictionary<string, string?>
            {
                ["userId"] = userId.ToString(),
                ["email"] = userEmail,
                ["sku"] = sku,
                ["qty"] = lineItem.Quantity?.ToString() ?? "1",
                ["priceId"] = priceId
            },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl
        };

        _log.LogInformation("CreateOneTimeCheckout: user {UserId} priceId={PriceId} sku={Sku} qty={Qty}",
            userId, priceId, sku, lineItem.Quantity);

        var sessSvc = new Checkout.SessionService(_client);      // CHANGED: pass client
        var session = await sessSvc.CreateAsync(create);
        return new CheckoutSession(session.Url);
    }

    // ---------- Webhook ----------

    public async Task<(string eventId, int? userId, string? customerRef, string? subscriptionRef,
                       string? planKey, string? status, DateTime? periodEndUtc,
                       DateTime? periodStartUtc, DateTime? cancelAtUtc,
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
            throw;
        }

        _log.LogInformation("Stripe event received: type={Type} id={Id}", stripeEvent.Type, stripeEvent.Id);

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                {
                    var session = (Checkout.Session)stripeEvent.Data.Object;
                    var uidStr = session.ClientReferenceId ?? session.Metadata.GetValueOrDefault("userId");

                    if (string.Equals(session.Mode, "subscription", StringComparison.OrdinalIgnoreCase))
                    {
                        // Fetch subscription + latest invoice to derive current period
                        Subscription? sub = null;
                        if (!string.IsNullOrEmpty(session.SubscriptionId))
                        {
                            var subSvc = new SubscriptionService(_client);      // CHANGED
                            sub = await subSvc.GetAsync(session.SubscriptionId);
                        }

                        Invoice? latestInv = null;
                        if (!string.IsNullOrEmpty(sub?.LatestInvoiceId))
                        {
                            var invSvc = new InvoiceService(_client);          // CHANGED
                            latestInv = await invSvc.GetAsync(sub.LatestInvoiceId);
                        }
                        var line = latestInv?.Lines?.Data?.FirstOrDefault();
                        var per = line?.Period;

                        return (
                            eventId: stripeEvent.Id,
                            userId: int.TryParse(uidStr, out var id) ? id : (int?)null,
                            customerRef: session.CustomerId,
                            subscriptionRef: session.SubscriptionId,
                            planKey: session.Metadata.GetValueOrDefault("plan") ?? "pro",
                            status: sub?.Status ?? "active",
                            periodEndUtc: per?.End.ToUniversalTime(),
                            periodStartUtc: per?.Start.ToUniversalTime(),
                            cancelAtUtc: sub?.CancelAt,
                            addOnSku: null,
                            addOnQty: 0
                        );
                    }

                    if (string.Equals(session.Mode, "payment", StringComparison.OrdinalIgnoreCase))
                    {
                        var sku = session.Metadata.GetValueOrDefault("sku");
                        var qtyStr = session.Metadata.GetValueOrDefault("qty");
                        var qty = int.TryParse(qtyStr, out var q) ? Math.Max(1, q) : 1;

                        // Fallback by priceId in metadata if sku missing/unknown
                        var priceIdMeta = session.Metadata.GetValueOrDefault("priceId");
                        if (string.IsNullOrEmpty(sku) || string.Equals(sku, "unknown", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrEmpty(priceIdMeta))
                            {
                                sku = priceIdMeta == _cfg.PriceIdAddon5 ? "addon_plus5" :
                                      priceIdMeta == _cfg.PriceIdAddon11 ? "addon_plus11" : "unknown";
                            }
                        }

                        _log.LogInformation("Webhook map: sessionId={SessionId} sku={Sku} qty={Qty} priceIdMeta={PriceIdMeta}",
                            session.Id, sku, qty, priceIdMeta);

                        return (
                            eventId: stripeEvent.Id,
                            userId: int.TryParse(uidStr, out var id) ? id : (int?)null,
                            customerRef: session.CustomerId,
                            subscriptionRef: null,
                            planKey: null,
                            status: "paid",
                            periodEndUtc: null,
                            periodStartUtc: null,
                            cancelAtUtc: null,
                            addOnSku: sku,
                            addOnQty: qty
                        );
                    }

                    return (
                        eventId: stripeEvent.Id,
                        userId: null,
                        customerRef: session.CustomerId,
                        subscriptionRef: session.SubscriptionId,
                        planKey: null,
                        status: "ignored",
                        periodEndUtc: null,
                        periodStartUtc: null,
                        cancelAtUtc: null,
                        addOnSku: null,
                        addOnQty: 0
                    );
                }

            case "customer.subscription.created":
            case "customer.subscription.updated":
                {
                    var sub = (Subscription)stripeEvent.Data.Object;

                    // Map plan from price id
                    var priceId = sub.Items?.Data?.FirstOrDefault()?.Price?.Id;
                    string? planKey =
                        priceId == _cfg.PriceIdPremium ? "premium" :
                        priceId == _cfg.PriceIdPro ? "pro" : null;

                    // Pull current period from latest invoice
                    Invoice? latestInv = null;
                    if (!string.IsNullOrEmpty(sub.LatestInvoiceId))
                    {
                        var invSvc = new InvoiceService(_client);              // CHANGED
                        latestInv = await invSvc.GetAsync(sub.LatestInvoiceId);
                    }
                    var line = latestInv?.Lines?.Data?.FirstOrDefault();
                    var per = line?.Period;

                    return (
                        eventId: stripeEvent.Id,
                        userId: null,
                        customerRef: sub.CustomerId,
                        subscriptionRef: sub.Id,
                        planKey: planKey,
                        status: sub.Status,
                        periodEndUtc: per?.End.ToUniversalTime(),
                        periodStartUtc: per?.Start.ToUniversalTime(),
                        cancelAtUtc: sub.CancelAt,
                        addOnSku: null,
                        addOnQty: 0
                    );
                }

            case "customer.subscription.deleted":
                {
                    var sub = (Subscription)stripeEvent.Data.Object;

                    // Even on delete, latest invoice holds the last billed window
                    Invoice? latestInv = null;
                    if (!string.IsNullOrEmpty(sub.LatestInvoiceId))
                    {
                        var invSvc = new InvoiceService(_client);              // CHANGED
                        latestInv = await invSvc.GetAsync(sub.LatestInvoiceId);
                    }
                    var line = latestInv?.Lines?.Data?.FirstOrDefault();
                    var per = line?.Period;

                    return (
                        eventId: stripeEvent.Id,
                        userId: null,
                        customerRef: sub.CustomerId,
                        subscriptionRef: sub.Id,
                        planKey: "free",
                        status: "canceled",
                        periodEndUtc: per?.End.ToUniversalTime(),
                        periodStartUtc: per?.Start.ToUniversalTime(),
                        cancelAtUtc: sub.CancelAt,
                        addOnSku: null,
                        addOnQty: 0
                    );
                }

            case "invoice.payment_succeeded":
                {
                    var inv = (Invoice)stripeEvent.Data.Object;
                    var line = inv.Lines?.Data?.FirstOrDefault();
                    var p = line?.Period;

                    return (
                        eventId: stripeEvent.Id,
                        userId: null,
                        customerRef: inv.CustomerId,
                        subscriptionRef: null,
                        planKey: null,
                        status: "active",
                        periodEndUtc: p?.End.ToUniversalTime(),
                        periodStartUtc: p?.Start.ToUniversalTime(),
                        cancelAtUtc: null,
                        addOnSku: null,
                        addOnQty: 0
                    );
                }

            default:
                return (
                    eventId: stripeEvent.Id,
                    userId: null,
                    customerRef: null,
                    subscriptionRef: null,
                    planKey: null,
                    status: "ignored",
                    periodEndUtc: null,
                    periodStartUtc: null,
                    cancelAtUtc: null,
                    addOnSku: null,
                    addOnQty: 0
                );
        }
    }
}