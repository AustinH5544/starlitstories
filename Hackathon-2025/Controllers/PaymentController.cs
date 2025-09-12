using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Hackathon_2025.Services;                 // NEW: IQuotaService, IPeriodService, IPaymentGateway
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;            // NEW: IOptions<StripeSettings>
using Stripe;
using System.Security.Claims;
using Microsoft.Extensions.Logging; // at top

// so IPaymentGateway resolves

namespace Hackathon_2025.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentGateway _gateway;
        private readonly AppDbContext _db;
        private readonly ILogger<PaymentsController> _log; // NEW

        // NEW: policy + period services and Stripe price IDs
        private readonly IQuotaService _quota;          // NEW

        private readonly IPeriodService _period;        // NEW
        private readonly IOptions<StripeSettings> _stripe; // NEW

        public PaymentsController(
            IPaymentGateway gateway,
            AppDbContext db,
            IQuotaService quota,                        // NEW
            IPeriodService period,                      // NEW
            IOptions<StripeSettings> stripe,
            ILogger<PaymentsController> log)            // NEW
        {
            _gateway = gateway;
            _db = db;
            _log = log; // NEW
            _quota = quota;                             // NEW
            _period = period;                           // NEW
            _stripe = stripe;                           // NEW
        }

        [HttpPost("create-checkout-session")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CheckoutRequest request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Membership))
                return BadRequest("Membership is required.");

            // Get user id from any of the usual claim names
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

            // Use your real frontend domain here when ready
            var domain = "http://localhost:5173"; // dev
            var successUrl = $"{domain}/profile?upgraded=1&plan={request.Membership}";
            var cancelUrl = $"{domain}/upgrade?cancelled=1";

            var session = await _gateway.CreateCheckoutSessionAsync(userId, email, request.Membership, successUrl, cancelUrl);

            return Ok(new { checkoutUrl = session.Url });
        }

        // NEW: one-time add-on credit purchase (premium-only, optionally only when base is exhausted)
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

            // 2) Period rollover (ensures BooksGenerated is current)
            var now = DateTime.UtcNow;
            if (_period.IsPeriodBoundary(user, now))
            {
                _period.OnPeriodRollover(user, now);
                _db.Users.Update(user);
                await _db.SaveChangesAsync();
            }

            // 3) Policy gates: premium-only + only-when-exhausted (configurable)
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

            // 4) Map pack -> Stripe Price ID
            var pack = (req?.Pack ?? "plus5").ToLowerInvariant();
            var quantity = Math.Max(1, req?.Quantity ?? 1);

            string? priceId = pack switch
            {
                "plus5" => _stripe.Value.PriceIdAddon5,
                "plus11" => _stripe.Value.PriceIdAddon11,
                _ => null
            };
            if (priceId is null) return BadRequest("Unknown credit pack.");

            // Get an email (prefer claim, else DB)
            var email = User.FindFirst("email")?.Value ?? user.Email;

            // 5) Create a one-time Checkout Session
            var domain = "http://localhost:5173"; // dev; replace in prod
            var successUrl = $"{domain}/profile?credits=1";
            var cancelUrl = $"{domain}/profile?cancelled=1";

            var session = await _gateway.CreateOneTimeCheckoutAsync(userId, email, priceId, quantity, successUrl, cancelUrl);

            return Ok(new { checkoutUrl = session.Url });
        }

        [HttpGet("billing/portal")]
        [Authorize] // ensure only signed-in users hit this
        public async Task<IActionResult> BillingPortal()
        {
            // Get user id from any of the common claim names
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
                // e.g., no BillingCustomerRef yet
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                // log ex as needed
                return Problem("Could not create billing portal session.");
            }
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
                // e.g., no active subscription
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
                var (eventId, uid, custRef, subRef, planKey, status, periodEnd, addOnSku, addOnQty) =
                    await _gateway.HandleWebhookAsync(Request);

                _log.LogInformation(
                    "Stripe webhook {EventId} uid={Uid} cust={Cust} sub={Sub} planKey={Plan} status={Status} addOn={Sku}/{Qty}",
                    eventId, uid, custRef, subRef, planKey, status, addOnSku, addOnQty
                );

                // --- No idempotency fence (for testing). ---
                // Exit early for informational/no-op events to avoid unnecessary work.
                if (string.Equals(status, "ignored", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrEmpty(addOnSku)
                    && string.IsNullOrEmpty(planKey))
                {
                    _log.LogInformation("Webhook {EventId}: nothing to apply (status=ignored).", eventId);
                    return Ok();
                }

                // --- Resolve user via uid -> customer -> subscription ---
                User? user = null;
                if (uid.HasValue)
                    user = await _db.Users.FindAsync(uid.Value);
                if (user is null && !string.IsNullOrEmpty(custRef))
                    user = await _db.Users.FirstOrDefaultAsync(u => u.BillingCustomerRef == custRef);
                if (user is null && !string.IsNullOrEmpty(subRef))
                    user = await _db.Users.FirstOrDefaultAsync(u => u.BillingSubscriptionRef == subRef);

                if (user is null)
                {
                    _log.LogWarning("Webhook {EventId}: user not found (uid={Uid}, cust={Cust}, sub={Sub})",
                        eventId, uid, custRef, subRef);
                    return Ok(); // still 200 so Stripe won't retry forever
                }

                _log.LogInformation("Webhook {EventId}: applying updates to user {UserId}", eventId, user.Id);

                // --- Subscription/membership updates (if present) ---
                user.BillingProvider = "stripe";
                if (!string.IsNullOrEmpty(custRef)) user.BillingCustomerRef = custRef;
                if (!string.IsNullOrEmpty(subRef)) user.BillingSubscriptionRef = subRef;
                if (!string.IsNullOrEmpty(planKey)) { user.PlanKey = planKey; user.Membership = planKey; }
                if (!string.IsNullOrEmpty(status)) user.PlanStatus = status;
                if (periodEnd.HasValue) user.CurrentPeriodEndUtc = periodEnd;

                // --- One-time add-on credits (if present) ---
                if (!string.IsNullOrEmpty(addOnSku) && addOnQty > 0)
                {
                    var qty = Math.Max(1, addOnQty);
                    var perPack = addOnSku switch
                    {
                        "addon_plus5" => 5,
                        "addon_plus11" => 11,
                        _ => 0
                    };

                    if (perPack == 0)
                    {
                        _log.LogWarning("Webhook {EventId}: Unknown add-on SKU '{Sku}'. No credits added.", eventId, addOnSku);
                    }
                    else
                    {
                        var before = user.AddOnBalance;
                        var credits = perPack * qty;

                        // IDEMPOTENCY DISABLED: duplicates from Stripe will add credits again.
                        user.AddOnBalance = checked(before + credits);

                        _log.LogInformation(
                            "Webhook {EventId}: +{Credits} credits ({Sku} x{Qty}) → user {UserId} balance {Before} → {After}",
                            eventId, credits, addOnSku, qty, user.Id, before, user.AddOnBalance
                        );
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(addOnSku))
                        _log.LogDebug("Webhook {EventId}: No add-on SKU provided.", eventId);
                    if (addOnQty <= 0)
                        _log.LogDebug("Webhook {EventId}: Non-positive add-on quantity {Qty}.", eventId, addOnQty);
                }

                await _db.SaveChangesAsync();

                _log.LogInformation(
                    "Webhook {EventId}: updated user {UserId} → membership={Membership}, status={Status}",
                    eventId, user.Id, user.Membership, user.PlanStatus
                );

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