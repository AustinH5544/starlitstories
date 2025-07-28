using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Controllers;
using Hackathon_2025.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Stripe.Checkout;
using System.Collections.Generic;
using System.Text.Json;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class PaymentsControllerTests
{
    private StripeSettings _fakeSettings = null!;
    private IOptions<StripeSettings> _mockOptions = null!;
    private Mock<SessionService> _mockSessionService = null!;
    private PaymentsController _controller = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange fake Stripe settings
        _fakeSettings = new StripeSettings
        {
            SecretKey = "sk_test_fake",
            PublishableKey = "pk_test_fake"
        };

        // Create mock options and mock session service
        _mockOptions = Options.Create(_fakeSettings);
        _mockSessionService = new Mock<SessionService>();

        // Inject both into controller
        _controller = new PaymentsController(_mockOptions, _mockSessionService.Object);
    }

    [TestMethod]
    public void CreateCheckoutSession_ProMembership_ReturnsCheckoutUrl()
    {
        // Arrange
        var request = new CheckoutRequest
        {
            Email = "pro@example.com",
            Membership = "pro"
        };

        var mockUrl = "https://stripe.com/checkout/session123";
        var mockSession = new Session { Url = mockUrl };

        var sessionServiceMock = new Mock<SessionService>();
        sessionServiceMock
            .Setup(s => s.Create(It.IsAny<SessionCreateOptions>(), null))
            .Returns(mockSession);

        var controller = new PaymentsController(_mockOptions, sessionServiceMock.Object);

        // Act
        var result = controller.CreateCheckoutSession(request) as OkObjectResult;

        // Assert
        Assert.IsNotNull(result);

        var json = JsonSerializer.Serialize(result.Value);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

        Assert.IsNotNull(parsed);
        Assert.AreEqual(mockUrl, parsed["checkoutUrl"]);
    }

    [TestMethod]
    public void CreateCheckoutSession_InvalidMembership_ReturnsBadRequest()
    {
        // Arrange
        var request = new CheckoutRequest
        {
            Email = "test@example.com",
            Membership = "invalid"
        };

        // Act
        var result = _controller.CreateCheckoutSession(request) as BadRequestObjectResult;

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Invalid plan", result.Value);
    }
}
