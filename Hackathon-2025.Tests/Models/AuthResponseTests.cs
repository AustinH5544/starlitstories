using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;

namespace Hackathon2025.Tests.Models;

[TestClass]
public class AuthResponseTests
{
    [TestMethod]
    public void Constructor_ValidValues_SetsPropertiesCorrectly()
    {
        // Arrange
        var email = "user@example.com";
        var membership = MembershipPlan.Pro;

        // Act
        var response = new AuthResponse
        {
            Email = email,
            Membership = membership
        };

        // Assert
        Assert.AreEqual(email, response.Email);
        Assert.AreEqual(membership, response.Membership);
    }

    [TestMethod]
    public void Membership_AssignedEnumValue_IsStoredCorrectly()
    {
        // Arrange
        var membership = MembershipPlan.Premium;

        // Act
        var response = new AuthResponse
        {
            Email = "test@example.com",
            Membership = membership
        };

        // Assert
        Assert.AreEqual(MembershipPlan.Premium, response.Membership);
    }

    [TestMethod]
    public void Email_AssignedValidEmail_SetsCorrectValue()
    {
        // Arrange
        var email = "valid@example.com";

        // Act
        var response = new AuthResponse
        {
            Email = email,
            Membership = MembershipPlan.Free
        };

        // Assert
        Assert.AreEqual(email, response.Email);
    }
}
