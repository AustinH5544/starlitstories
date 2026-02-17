using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;

namespace Hackathon2025.Tests.Models;

[TestClass]
public class AuthRequestTests
{
    [TestMethod]
    public void Constructor_ValidValues_SetsPropertiesCorrectly()
    {
        // Arrange
        var email = "test@example.com";
        var password = "secure123!";

        // Act
        var request = new AuthRequest
        {
            Email = email,
            Password = password
            // Membership intentionally omitted to use the default (null)
        };

        // Assert
        Assert.AreEqual(email, request.Email);
        Assert.AreEqual(password, request.Password);
        Assert.IsNull(request.Membership);
    }

    [TestMethod]
    public void Membership_Default_WhenOmitted_IsNull()
    {
        // Arrange
        var email = "test@example.com";
        var password = "secure123!";

        // Act
        var request = new AuthRequest
        {
            Email = email,
            Password = password
            // Membership not set
        };

        // Assert
        Assert.IsNull(request.Membership);
    }

    [TestMethod]
    public void Membership_WhenExplicitlyInitializedToNull_AllowsNullValue()
    {
        // Arrange
        var email = "test@example.com";
        var password = "secure123!";

        // Act
        var request = new AuthRequest
        {
            Email = email,
            Password = password,
            Membership = null
        };

        // Assert
        Assert.AreEqual(email, request.Email);
        Assert.AreEqual(password, request.Password);
        Assert.IsNull(request.Membership);
    }
}
