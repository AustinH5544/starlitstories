using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;
using Microsoft.Extensions.Options;
using Hackathon_2025.Models;

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly StripeSettings _stripe;
    private readonly SessionService _sessionService;

    public PaymentsController(IOptions<StripeSettings> stripeOptions, SessionService sessionService)
    {
        _stripe = stripeOptions.Value;
        _sessionService = sessionService;
        Stripe.StripeConfiguration.ApiKey = _stripe.SecretKey;
    }

    [HttpPost("create-checkout-session")]
    public IActionResult CreateCheckoutSession([FromBody] CheckoutRequest request)
    {
        var domain = "http://localhost:5173"; // replace in production

        var lineItem = request.Membership switch
        {
            "pro" => new SessionLineItemOptions
            {
                Price = "price_1RQDReBVRvvlCoea7xGvFFtt",
                Quantity = 1
            },
            "premium" => new SessionLineItemOptions
            {
                Price = "price_1RQDSuBVRvvlCoea0OgsWr7C",
                Quantity = 1
            },
            _ => null
        };

        if (lineItem == null)
            return BadRequest("Invalid plan");

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions> { lineItem },
            Mode = "subscription",
            CustomerEmail = request.Email,
            SuccessUrl = $"{domain}/signup/complete?email={request.Email}&plan={request.Membership}",
            CancelUrl = $"{domain}/signup?cancelled=true"
        };

        var session = _sessionService.Create(options);

        return Ok(new { checkoutUrl = session.Url });
    }
}