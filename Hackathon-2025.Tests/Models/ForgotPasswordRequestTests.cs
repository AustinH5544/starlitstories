using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;

namespace Hackathon_2025.Tests.Models;

[TestClass]
public class ForgotPasswordRequestTests
{
    [TestMethod]
    public void Constructor_WithValidEmail_SetsEmailCorrectly()
    {
        // Arrange
        var email = "user@example.com";

        // Act
        var request = new ForgotPasswordRequest
        {
            Email = email
        };

        // Assert
        Assert.AreEqual(email, request.Email);
    }

    [TestMethod]
    public void Constructor_RequiresEmailProperty()
    {
        var request = new ForgotPasswordRequest
        {
            Email = "test@example.com"
        };

        // Assert
        Assert.IsNotNull(request.Email);
    }
}
