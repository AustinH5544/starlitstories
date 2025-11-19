using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;

namespace Hackathon_2025.Tests.Models;

[TestClass]
public class StripeSettingsTests
{
    [TestMethod]
    public void PropertyAssignment_ValidValues_AreStoredCorrectly()
    {
        // Arrange
        var settings = new StripeSettings
        {
            SecretKey = "sk_test_12345",
            PublishableKey = "pk_test_67890",
            WebhookSecret = "whsec_abcdef",
            PriceIdPro = "price_pro_001",
            PriceIdPremium = "price_premium_002",
            PriceIdAddon5 = "price_addon5_003",
            PriceIdAddon11 = "price_addon11_004"
        };

        // Act
        var secret = settings.SecretKey;
        var publishable = settings.PublishableKey;
        var webhook = settings.WebhookSecret;
        var pro = settings.PriceIdPro;
        var premium = settings.PriceIdPremium;
        var addon5 = settings.PriceIdAddon5;
        var addon11 = settings.PriceIdAddon11;

        // Assert
        Assert.AreEqual("sk_test_12345", secret);
        Assert.AreEqual("pk_test_67890", publishable);
        Assert.AreEqual("whsec_abcdef", webhook);
        Assert.AreEqual("price_pro_001", pro);
        Assert.AreEqual("price_premium_002", premium);
        Assert.AreEqual("price_addon5_003", addon5);
        Assert.AreEqual("price_addon11_004", addon11);
    }
}
