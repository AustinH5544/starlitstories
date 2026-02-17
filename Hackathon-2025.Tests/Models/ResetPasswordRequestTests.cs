using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;

namespace Hackathon_2025.Tests.Models;

[TestClass]
public class ResetPasswordRequestTests
{
    [TestMethod]
    public void Constructor_ValidValues_SetsPropertiesCorrectly()
    {
        // Arrange
        var email = "reset@example.com";
        var token = "abc123token";
        var password = "NewSecurePassword!";

        // Act
        var request = new ResetPasswordRequest
        {
            Email = email,
            Token = token,
            NewPassword = password
        };

        // Assert
        Assert.AreEqual(email, request.Email);
        Assert.AreEqual(token, request.Token);
        Assert.AreEqual(password, request.NewPassword);
    }

    [TestMethod]
    public void NewPassword_AssignedDifferentValidValue_IsStoredCorrectly()
    {
        // Arrange
        var email = "user2@example.com";
        var token = "differentToken456";
        var password = "AnotherSecurePassword!";

        // Act
        var request = new ResetPasswordRequest
        {
            Email = email,
            Token = token,
            NewPassword = password
        };

        // Assert
        Assert.AreEqual("user2@example.com", request.Email);
        Assert.AreEqual("differentToken456", request.Token);
        Assert.AreEqual("AnotherSecurePassword!", request.NewPassword);
    }
}
