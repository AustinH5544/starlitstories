using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Microsoft.AspNetCore.Mvc;

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
        var domain = "http://localhost:5173"; // replace per env
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        var email = User.FindFirst("email")!.Value;

        var session = await _gateway.CreateCheckoutSessionAsync(
            userId, email, request.Membership,
            $"{domain}/signup/complete?plan={request.Membership}",
            $"{domain}/signup?cancelled=true"
        );

        return Ok(new { checkoutUrl = session.Url });
    }

    [HttpGet("billing/portal")]
    public async Task<IActionResult> BillingPortal()
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        var portal = await _gateway.CreatePortalSessionAsync(userId);
        return Ok(new { url = portal.Url });
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel()
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        await _gateway.CancelAtPeriodEndAsync(userId);
        return Ok(new { message = "Cancellation scheduled at period end." });
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