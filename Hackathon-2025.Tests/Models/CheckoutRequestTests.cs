using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;

namespace Hackathon_2025.Tests.Models;

[TestClass]
public class CheckoutRequestTests
{
    [TestMethod]
    public void DefaultConstructor_SetsExpectedDefaults()
    {
        // Arrange
        var request = new CheckoutRequest();

        // Assert
        Assert.AreEqual("", request.Membership);
    }

    [TestMethod]
    public void CanAssignValues()
    {
        // Arrange
        var request = new CheckoutRequest
        {
            Membership = "Premium"
        };

        // Assert
        Assert.AreEqual("Premium", request.Membership);
    }
}
