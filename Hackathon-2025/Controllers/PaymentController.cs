using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Hackathon_2025.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Stripe;
using System.Security.Claims;

namespace Hackathon_2025.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentGateway _gateway;
        private readonly AppDbContext _db;
        private readonly ILogger<PaymentsController> _log;

        private readonly IQuotaService _quota;
        private readonly IPeriodService _period;

        // Use concrete options values for easy access
        private readonly StripeOptions _stripe;

        private readonly AppOptions _app;

        public PaymentsController(
            IPaymentGateway gateway,
            AppDbContext db,
            IQuotaService quota,
            IPeriodService period,
            IOptions<StripeOptions> stripe,
            IOptions<AppOptions> app,
            ILogger<PaymentsController> log)
        {
            _gateway = gateway;
            _db = db;
            _log = log;
            _quota = quota;
            _period = period;
            _stripe = stripe.Value;   // <— StripeOptions
            _app = app.Value;         // <— AppOptions (BaseUrl, etc.)
        }

        [HttpPost("create-checkout-session")]
        [Authorize] // recommended: you rely on user claims
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CheckoutRequest request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Membership))
                return BadRequest("Membership is required.");

            // Resolve user id
            var userIdStr =
                User.FindFirst("sub")?.Value ??
                User.FindFirst("id")?.Value ??
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized("No user id claim on the request.");

            // Get an email (prefer claim, else DB)
            var email = User.FindFirst("email")?.Value;
            if (string.IsNullOrWhiteSpace(email))
            {
                var userFromDb = await _db.Users.FindAsync(userId);
                if (userFromDb == null) return Unauthorized("User not found.");
                email = userFromDb.Email;
            }

            // Build absolute URLs from AppOptions.BaseUrl
            var baseUrl = (_app.BaseUrl ?? "http://localhost:5173").TrimEnd('/');
            var successUrl = $"{baseUrl}/profile?upgraded=1&plan={request.Membership}";
            var cancelUrl = $"{baseUrl}/upgrade?cancelled=1";

            var session = await _gateway.CreateCheckoutSessionAsync(
                userId, email, request.Membership, successUrl, cancelUrl);

            return Ok(new { checkoutUrl = session.Url });
        }

        // One-time add-on credit purchase
        [HttpPost("buy-credits")]
        [Authorize]
        public async Task<IActionResult> BuyCredits([FromBody] BuyCreditsRequest req)
        {
            // 1) Resolve user
            var userIdStr =
                User.FindFirst("sub")?.Value ??
                User.FindFirst("id")?.Value ??
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized("No user id claim on the request.");

            var user = await _db.Users.FindAsync(userId);
            if (user is null) return Unauthorized("User not found.");

            // 2) Period rollover
            var now = DateTime.UtcNow;
            if (_period.IsPeriodBoundary(user, now))
            {
                _period.OnPeriodRollover(user, now);
                _db.Users.Update(user);
                await _db.SaveChangesAsync();
            }

            // 3) Policy gates
            var baseQuota = _quota.BaseQuotaFor(user.Membership);
            var baseRemaining = Math.Max(baseQuota - user.BooksGenerated, 0);

            if (_quota.RequirePremiumForAddons() &&
                !string.Equals(user.Membership, "premium", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, "Add-on credits are only available to premium members. Please upgrade first.");
            }

            if (_quota.OnlyAllowPurchaseWhenExhausted() && baseRemaining > 0)
            {
                return BadRequest($"You still have {baseRemaining} base story slot(s) remaining this period.");
            }

            // 4) Map pack -> Stripe Price ID (from StripeOptions)
            var pack = (req?.Pack ?? "plus5").ToLowerInvariant();
            var quantity = Math.Max(1, req?.Quantity ?? 1);

            string? priceId = pack switch
            {
                "plus5" => _stripe.PriceIdAddon5,
                "plus11" => _stripe.PriceIdAddon11,
                _ => null
            };
            if (priceId is null) return BadRequest("Unknown credit pack.");

            // Get email
            var email = User.FindFirst("email")?.Value ?? user.Email;

            // 5) Build URLs from AppOptions
            var baseUrl = (_app.BaseUrl ?? "http://localhost:5173").TrimEnd('/');
            var successUrl = $"{baseUrl}/profile?credits=1";
            var cancelUrl = $"{baseUrl}/profile?cancelled=1";

            var session = await _gateway.CreateOneTimeCheckoutAsync(userId, email, priceId, quantity, successUrl, cancelUrl);
            return Ok(new { checkoutUrl = session.Url });
        }

        [HttpGet("billing/portal")]
        [Authorize]
        public async Task<IActionResult> BillingPortal()
        {
            var userIdStr =
                User.FindFirst("sub")?.Value ??
                User.FindFirst("id")?.Value ??
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized("No user id claim on the request.");

            try
            {
                var portal = await _gateway.CreatePortalSessionAsync(userId);
                return Ok(new { url = portal.Url });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return Problem("Could not create billing portal session.");
            }
        }

        [Authorize]
        [HttpGet("subscription")]
        public async Task<IActionResult> GetSubscription()
        {
            var userIdStr =
                User.FindFirst("sub")?.Value ??
                User.FindFirst("id")?.Value ??
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized("No user id claim on the request.");

            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return Unauthorized("User not found.");

            var payload = new
            {
                subscription = new
                {
                    status = user.PlanStatus,
                    planKey = user.PlanKey,
                    currentPeriodStart = user.CurrentPeriodStartUtc?.ToUniversalTime(),
                    currentPeriodEnd = user.CurrentPeriodEndUtc?.ToUniversalTime(),
                    cancelAt = user.CancelAtUtc?.ToUniversalTime(),
                    subscriptionRef = user.BillingSubscriptionRef,
                    customerRef = user.BillingCustomerRef
                }
            };

            return Ok(payload);
        }

        [HttpPost("cancel")]
        [Authorize]
        public async Task<IActionResult> Cancel()
        {
            var userIdStr =
                User.FindFirst("sub")?.Value ??
                User.FindFirst("id")?.Value ??
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized("No user id claim on the request.");

            try
            {
                await _gateway.CancelAtPeriodEndAsync(userId);
                return Ok(new { message = "Cancellation scheduled. You'll retain access until the current period ends." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return Problem("Could not schedule cancellation.");
            }
        }

        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook()
        {
            try
            {
                var (eventId, uid, custRef, subRef, planKey, status, periodEnd, periodStart, cancelAt, addOnSku, addOnQty) =
                    await _gateway.HandleWebhookAsync(Request);

                _log.LogInformation(
                    "Stripe webhook {EventId} uid={Uid} cust={Cust} sub={Sub} planKey={Plan} status={Status} addOn={Sku}/{Qty}",
                    eventId, uid, custRef, subRef, planKey, status, addOnSku, addOnQty);

                // 1) Ignore events we never mutate on
                var actionable =
                    (!string.IsNullOrEmpty(addOnSku) && addOnQty > 0) ||              // one-time add-ons
                    !string.IsNullOrEmpty(planKey) ||                                 // subscription price mapped to plan
                    string.Equals(status, "active", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status, "canceled", StringComparison.OrdinalIgnoreCase);

                if (!actionable)
                {
                    _log.LogInformation("Webhook {EventId}: nothing to apply.", eventId);
                    return Ok();
                }

                // 2) Atomic fence + updates under EF retry strategy
                var strategy = _db.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _db.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                    // --- Idempotency: insert-if-not-exists ---
                    // If the row existed already, MERGE returns 0 rows affected.
                    var rows = await _db.Database.ExecuteSqlInterpolatedAsync($@"
MERGE [dbo].[ProcessedWebhooks] WITH (HOLDLOCK, ROWLOCK) AS t
USING (SELECT {eventId} AS [EventId], SYSUTCDATETIME() AS [ProcessedAtUtc]) AS s
ON (t.[EventId] = s.[EventId])
WHEN NOT MATCHED THEN
    INSERT ([EventId], [ProcessedAtUtc]) VALUES (s.[EventId], s.[ProcessedAtUtc]);");

                    if (rows == 0)
                    {
                        _log.LogInformation("Webhook {EventId}: already processed. Skipping.", eventId);
                        await tx.CommitAsync();
                        return;
                    }

                    // --- Resolve user ---
                    User? user = null;
                    if (uid.HasValue)
                        user = await _db.Users.FindAsync(uid.Value);
                    if (user is null && !string.IsNullOrEmpty(custRef))
                        user = await _db.Users.FirstOrDefaultAsync(u => u.BillingCustomerRef == custRef);
                    if (user is null && !string.IsNullOrEmpty(subRef))
                        user = await _db.Users.FirstOrDefaultAsync(u => u.BillingSubscriptionRef == subRef);

                    if (user is null)
                    {
                        // Pick ONE policy:
                        // (A) Consider consumed (prevents replays — what you do now):
                        _log.LogWarning("Webhook {EventId}: user not found (uid={Uid}, cust={Cust}, sub={Sub}) — consumed.",
                            eventId, uid, custRef, subRef);
                        await tx.CommitAsync();
                        return;

                        // (B) Or: rollback so the fence doesn’t stick, then 500 to trigger Stripe retry:
                        // _log.LogWarning("Webhook {EventId}: user not found — will retry via Stripe.", eventId);
                        // await tx.RollbackAsync();
                        // throw new Exception("User not found for actionable event");
                    }

                    // --- Apply updates ---
                    user.BillingProvider = "stripe";
                    if (!string.IsNullOrEmpty(custRef)) user.BillingCustomerRef = custRef;
                    if (!string.IsNullOrEmpty(subRef)) user.BillingSubscriptionRef = subRef;
                    if (!string.IsNullOrEmpty(planKey)) { user.PlanKey = planKey; user.Membership = planKey; }
                    if (!string.IsNullOrEmpty(status)) user.PlanStatus = status;
                    if (periodStart.HasValue) user.CurrentPeriodStartUtc = periodStart;
                    if (periodEnd.HasValue) user.CurrentPeriodEndUtc = periodEnd;
                    user.CancelAtUtc = cancelAt;

                    if (!string.IsNullOrEmpty(addOnSku) && addOnQty > 0)
                    {
                        int perPack = addOnSku switch { "addon_plus5" => 5, "addon_plus11" => 11, _ => 0 };
                        if (perPack > 0)
                        {
                            var before = user.AddOnBalance;
                            var credits = perPack * Math.Max(1, addOnQty);
                            user.AddOnBalance = checked(before + credits);
                            _log.LogInformation(
                                "Webhook {EventId}: +{Credits} ({Sku} x{Qty}) → {Before} → {After} for user {UserId}",
                                eventId, credits, addOnSku, addOnQty, before, user.AddOnBalance, user.Id);
                        }
                        else
                        {
                            _log.LogWarning("Webhook {EventId}: Unknown SKU {Sku}.", eventId, addOnSku);
                        }
                    }

                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();

                    _log.LogInformation("Webhook {EventId}: updated user {UserId} → membership={Membership}, status={Status}",
                        eventId, user.Id, user.Membership, user.PlanStatus);
                });

                return Ok();
            }
            catch (StripeException ex)
            {
                _log.LogError(ex, "Stripe webhook error");
                return BadRequest();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Unhandled webhook error");
                return StatusCode(500);
            }
        }
    }
}