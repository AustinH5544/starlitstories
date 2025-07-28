using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;

namespace Hackathon_2025.Tests.Models;

[TestClass]
public class ResetPasswordRequestTests
{
    [TestMethod]
    public void Constructor_DefaultValues_ContainEmptyStrings()
    {
        // Arrange
        var request = new ResetPasswordRequest();

        // Act
        var email = request.Email;
        var token = request.Token;
        var password = request.NewPassword;

        // Assert
        Assert.AreEqual(string.Empty, email);
        Assert.AreEqual(string.Empty, token);
        Assert.AreEqual(string.Empty, password);
    }

    [TestMethod]
    public void PropertyAssignment_CustomValues_AreStoredCorrectly()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "reset@example.com",
            Token = "abc123token",
            NewPassword = "NewSecurePassword!"
        };

        // Act
        var email = request.Email;
        var token = request.Token;
        var password = request.NewPassword;

        // Assert
        Assert.AreEqual("reset@example.com", email);
        Assert.AreEqual("abc123token", token);
        Assert.AreEqual("NewSecurePassword!", password);
    }
}
