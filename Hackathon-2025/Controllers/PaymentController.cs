using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentGateway _gateway;
    private readonly AppDbContext _db;

    public PaymentsController(IPaymentGateway gateway, AppDbContext db)
    {
        _gateway = gateway;
        _db = db;
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
        catch (Exception ex)
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
    public async Task<IActionResult> Webhook()
    {
        var (uid, custRef, subRef, planKey, status, periodEnd) =
            await _gateway.HandleWebhookAsync(Request);

        // Try to locate the user:
        User? user = null;
        if (uid.HasValue) user = await _db.Users.FindAsync(uid.Value);
        if (user is null && !string.IsNullOrEmpty(custRef))
            user = _db.Users.FirstOrDefault(u => u.BillingCustomerRef == custRef);
        if (user is null && !string.IsNullOrEmpty(subRef))
            user = _db.Users.FirstOrDefault(u => u.BillingSubscriptionRef == subRef);

        if (user != null)
        {
            user.BillingProvider = "stripe";
            if (!string.IsNullOrEmpty(custRef)) user.BillingCustomerRef = custRef;
            if (!string.IsNullOrEmpty(subRef)) user.BillingSubscriptionRef = subRef;
            if (!string.IsNullOrEmpty(planKey)) { user.PlanKey = planKey; user.Membership = planKey; } // keep in sync
            if (!string.IsNullOrEmpty(status)) user.PlanStatus = status;
            user.CurrentPeriodEndUtc = periodEnd;
            await _db.SaveChangesAsync();
        }

        return Ok();
    }
}