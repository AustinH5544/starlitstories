//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using Hackathon_2025.Controllers;
//using Hackathon_2025.Models;
//using Hackathon_2025.Data;
//using Hackathon_2025.Services;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Options;
//using Microsoft.Extensions.Logging;
//using Moq;
//using System.Threading.Tasks;
//using System;
//using System.Security.Claims;
//using Microsoft.AspNetCore.Http;
//using Stripe.Checkout;
//using Hackathon_2025.Options;

//namespace Hackathon_2025.Tests.Controllers;

//[TestClass]
//public class PaymentsControllerTests
//{
//    private Mock<IPaymentGateway> _mockGateway = null!;
//    private Mock<IQuotaService> _mockQuota = null!;
//    private Mock<IPeriodService> _mockPeriod = null!;
//    private Mock<ILogger<PaymentsController>> _mockLogger = null!;
//    private IOptions<StripeOptions> _stripeOptions = null!;
//    private IOptions<AppOptions> _appOptions = null!;
//    private AppDbContext _db = null!;
//    private PaymentsController _controller = null!;

//    private DefaultHttpContext _http = null!;

//    [TestInitialize]
//    public void Setup()
//    {
//        var options = new DbContextOptionsBuilder<AppDbContext>()
//            .UseInMemoryDatabase(Guid.NewGuid().ToString())
//            .Options;
//        _db = new AppDbContext(options);

//        _mockGateway = new Mock<IPaymentGateway>();
//        _mockQuota = new Mock<IQuotaService>();
//        _mockPeriod = new Mock<IPeriodService>();
//        _mockLogger = new Mock<ILogger<PaymentsController>>();

//        _stripeOptions = Options.Create(new StripeOptions
//        {
//            SecretKey = "sk_test",
//            PublishableKey = "pk_test",
//            WebhookSecret = "whsec_test",
//            PriceIdPro = "price_pro",
//            PriceIdPremium = "price_premium",
//            PriceIdAddon5 = "price_addon5",
//            PriceIdAddon11 = "price_addon11"
//        });

//        _appOptions = Options.Create(new AppOptions
//        {
//            BaseUrl = "http://localhost:5173",
//            AllowedCorsOrigins = "http://localhost:5173"
//        });

//        _controller = new PaymentsController(
//            _mockGateway.Object,
//            _db,
//            _mockQuota.Object,
//            _mockPeriod.Object,
//            _stripeOptions,
//            _appOptions,
//            _mockLogger.Object
//        );

//        // Authenticated user with id=1 and email claim
//        _http = new DefaultHttpContext();
//        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
//        {
//            new Claim(ClaimTypes.NameIdentifier, "1"),
//            new Claim("email", "test@example.com")
//        }, "mock"));
//        _http.User = user;

//        _controller.ControllerContext = new ControllerContext
//        {
//            HttpContext = _http
//        };

//        // Seed user
//        _db.Users.Add(new User
//        {
//            Id = 1,
//            Email = "test@example.com",
//            Membership = "premium",
//            PlanKey = "premium",
//            PlanStatus = "active",
//            CurrentPeriodStartUtc = DateTime.UtcNow.AddDays(-1),
//            CurrentPeriodEndUtc = DateTime.UtcNow.AddDays(29),
//            AddOnBalance = 0,
//            BooksGenerated = 0
//        });
//        _db.SaveChanges();
//    }

//    [TestMethod]
//    public async Task CreateCheckoutSession_ValidMembership_ReturnsCheckoutUrl()
//    {
//        // Arrange
//        var request = new CheckoutRequest { Membership = "premium" };
//        _mockGateway
//            .Setup(g => g.CreateCheckoutSessionAsync(
//                It.IsAny<int>(), It.IsAny<string>(), "premium",
//                It.IsAny<string>(), It.IsAny<string>()))
//            .ReturnsAsync(new Session { Url = "https://session.example/abc" });

//        // Act
//        var result = await _controller.CreateCheckoutSession(request) as OkObjectResult;

//        // Assert
//        Assert.IsNotNull(result);
//        dynamic payload = result.Value!;
//        Assert.AreEqual("https://session.example/abc", (string)payload.checkoutUrl);
//    }

//    [TestMethod]
//    public async Task CreateCheckoutSession_MissingMembership_ReturnsBadRequest()
//    {
//        // Arrange
//        var request = new CheckoutRequest { Membership = "" };

//        // Act
//        var result = await _controller.CreateCheckoutSession(request) as BadRequestObjectResult;

//        // Assert
//        Assert.IsNotNull(result);
//        Assert.AreEqual("Membership is required.", result.Value);
//    }

//    [TestMethod]
//    public async Task BuyCredits_ValidRequest_ReturnsCheckoutUrl()
//    {
//        // Arrange
//        var request = new BuyCreditsRequest { Pack = "plus5", Quantity = 2 };

//        // Policy: allow purchase regardless of remaining base quota
//        _mockQuota.Setup(q => q.BaseQuotaFor(It.IsAny<string>())).Returns(0);
//        _mockQuota.Setup(q => q.RequirePremiumForAddons()).Returns(false);
//        _mockQuota.Setup(q => q.OnlyAllowPurchaseWhenExhausted()).Returns(false);

//        _mockPeriod.Setup(p => p.IsPeriodBoundary(It.IsAny<User>(), It.IsAny<DateTime>()))
//                   .Returns(false);

//        _mockGateway
//            .Setup(g => g.CreateOneTimeCheckoutAsync(
//                1, "test@example.com", _stripeOptions.Value.PriceIdAddon5, 2,
//                It.IsAny<string>(), It.IsAny<string>()))
//            .ReturnsAsync(new Session { Url = "https://buy.example/xyz" });

//        // Act
//        var result = await _controller.BuyCredits(request) as OkObjectResult;

//        // Assert
//        Assert.IsNotNull(result);
//        dynamic payload = result.Value!;
//        Assert.AreEqual("https://buy.example/xyz", (string)payload.checkoutUrl);
//    }

//    [TestMethod]
//    public async Task BuyCredits_UnknownPack_ReturnsBadRequest()
//    {
//        // Arrange
//        var request = new BuyCreditsRequest { Pack = "nope" };

//        // Act
//        var result = await _controller.BuyCredits(request) as BadRequestObjectResult;

//        // Assert
//        Assert.IsNotNull(result);
//        Assert.AreEqual("Unknown credit pack.", result.Value);
//    }

//    [TestMethod]
//    public async Task BuyCredits_RequirePremiumGate_Returns403_WhenUserNotPremium()
//    {
//        // Arrange: downgrade user to free
//        var u = await _db.Users.FirstAsync(x => x.Id == 1);
//        u.Membership = "free";
//        _db.SaveChanges();

//        var request = new BuyCreditsRequest { Pack = "plus5", Quantity = 1 };

//        _mockQuota.Setup(q => q.RequirePremiumForAddons()).Returns(true);
//        _mockQuota.Setup(q => q.OnlyAllowPurchaseWhenExhausted()).Returns(false);
//        _mockQuota.Setup(q => q.BaseQuotaFor("free")).Returns(0);

//        // Act
//        var result = await _controller.BuyCredits(request) as ObjectResult;

//        // Assert
//        Assert.IsNotNull(result);
//        Assert.AreEqual(403, result.StatusCode);
//        Assert.AreEqual("Add-on credits are only available to premium members. Please upgrade first.", result.Value);
//    }

//    [TestMethod]
//    public async Task BuyCredits_OnlyAllowWhenExhausted_ReturnsBadRequest_WhenBaseRemaining()
//    {
//        // Arrange
//        var u = await _db.Users.FirstAsync(x => x.Id == 1);
//        u.Membership = "premium";
//        u.BooksGenerated = 1; // base quota 3 => remaining 2
//        _db.SaveChanges();

//        var request = new BuyCreditsRequest { Pack = "plus5", Quantity = 1 };

//        _mockQuota.Setup(q => q.RequirePremiumForAddons()).Returns(false);
//        _mockQuota.Setup(q => q.OnlyAllowPurchaseWhenExhausted()).Returns(true);
//        _mockQuota.Setup(q => q.BaseQuotaFor("premium")).Returns(3);

//        // Act
//        var result = await _controller.BuyCredits(request) as BadRequestObjectResult;

//        // Assert
//        Assert.IsNotNull(result);
//        Assert.AreEqual("You still have 2 base story slot(s) remaining this period.", result.Value);
//    }

//    [TestMethod]
//    public async Task BillingPortal_ReturnsUrl()
//    {
//        // Arrange
//        _mockGateway.Setup(g => g.CreatePortalSessionAsync(1))
//                    .ReturnsAsync(new PortalSession { Url = "https://portal.example/port" });

//        // Act
//        var result = await _controller.BillingPortal() as OkObjectResult;

//        // Assert
//        Assert.IsNotNull(result);
//        dynamic payload = result.Value!;
//        Assert.AreEqual("https://portal.example/port", (string)payload.url);
//    }

//    [TestMethod]
//    public async Task BillingPortal_InvalidOperation_ReturnsBadRequest()
//    {
//        // Arrange
//        _mockGateway.Setup(g => g.CreatePortalSessionAsync(1))
//                    .ThrowsAsync(new InvalidOperationException("no sub"));

//        // Act
//        var result = await _controller.BillingPortal() as BadRequestObjectResult;

//        // Assert
//        Assert.IsNotNull(result);
//        Assert.AreEqual("no sub", result.Value);
//    }

//    [TestMethod]
//    public async Task Cancel_SchedulesCancellation_ReturnsOk()
//    {
//        // Arrange
//        _mockGateway.Setup(g => g.CancelAtPeriodEndAsync(1))
//                    .Returns(Task.CompletedTask);

//        // Act
//        var result = await _controller.Cancel() as OkObjectResult;

//        // Assert
//        Assert.IsNotNull(result);
//        dynamic payload = result.Value!;
//        Assert.AreEqual("Cancellation scheduled. You'll retain access until the current period ends.", (string)payload.message);
//    }

//    [TestMethod]
//    public async Task GetSubscription_ReturnsPayload()
//    {
//        // Act
//        var result = await _controller.GetSubscription() as OkObjectResult;

//        // Assert
//        Assert.IsNotNull(result);
//        dynamic payload = result.Value!;
//        Assert.IsNotNull(payload.subscription);
//        Assert.AreEqual("active", (string)payload.subscription.status);
//        Assert.AreEqual("premium", (string)payload.subscription.planKey);
//    }

//    [TestMethod]
//    public async Task Webhook_AddOnCredits_IncrementsBalance()
//    {
//        // Arrange: mock gateway to return actionable add-on event (plus5 x2 => +10)
//        _mockGateway.Setup(g => g.HandleWebhookAsync(It.IsAny<HttpRequest>()))
//            .ReturnsAsync((
//                eventId: "evt_123",
//                uid: 1,
//                custRef: null,
//                subRef: null,
//                planKey: null,
//                status: null,
//                periodEnd: (DateTime?)null,
//                periodStart: (DateTime?)null,
//                cancelAt: (DateTime?)null,
//                addOnSku: "addon_plus5",
//                addOnQty: 2
//            ));

//        // Act
//        var result = await _controller.Webhook() as OkResult;

//        // Assert
//        Assert.IsNotNull(result);
//        var user = await _db.Users.FirstAsync(u => u.Id == 1);
//        Assert.AreEqual(10, user.AddOnBalance);
//    }

//    [TestMethod]
//    public async Task Webhook_NonActionable_ReturnsOk_NoChange()
//    {
//        // Arrange
//        var before = (await _db.Users.FirstAsync(u => u.Id == 1)).AddOnBalance;

//        _mockGateway.Setup(g => g.HandleWebhookAsync(It.IsAny<HttpRequest>()))
//            .ReturnsAsync((
//                eventId: "evt_ignored",
//                uid: (int?)null,
//                custRef: null,
//                subRef: null,
//                planKey: null,
//                status: "",              // empty + no add-on => non-actionable
//                periodEnd: (DateTime?)null,
//                periodStart: (DateTime?)null,
//                cancelAt: (DateTime?)null,
//                addOnSku: "",
//                addOnQty: 0
//            ));

//        // Act
//        var result = await _controller.Webhook() as OkResult;

//        // Assert
//        Assert.IsNotNull(result);
//        var after = (await _db.Users.FirstAsync(u => u.Id == 1)).AddOnBalance;
//        Assert.AreEqual(before, after);
//    }
//}
