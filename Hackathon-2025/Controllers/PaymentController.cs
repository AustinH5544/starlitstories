using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;
using Microsoft.Extensions.Options;
using Hackathon_2025.Models;

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IConfiguration _config;
    public PaymentsController(IConfiguration config)
    {
        _config = config;
        Stripe.StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
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

        var service = new SessionService();
        Session session = service.Create(options);

        return Ok(new { checkoutUrl = session.Url });
    }
}