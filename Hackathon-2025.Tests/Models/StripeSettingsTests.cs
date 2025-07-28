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
            PublishableKey = "pk_test_67890"
        };

        // Act
        var secret = settings.SecretKey;
        var publishable = settings.PublishableKey;

        // Assert
        Assert.AreEqual("sk_test_12345", secret);
        Assert.AreEqual("pk_test_67890", publishable);
    }
}
