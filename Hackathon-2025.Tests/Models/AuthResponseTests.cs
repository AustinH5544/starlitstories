using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;

namespace Hackathon2025.Tests.Models;

[TestClass]
public class AuthResponseTests
{
    [TestMethod]
    public void AuthResponse_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange
        AuthResponse response;

        // Act
        response = new AuthResponse();

        // Assert
        Assert.AreEqual(string.Empty, response.Email);
        Assert.AreEqual(string.Empty, response.Membership);
    }

    [TestMethod]
    public void AuthResponse_PropertySetters_AssignValuesCorrectly()
    {
        // Arrange
        AuthResponse response = new AuthResponse();

        // Act
        response.Email = "user@example.com";
        response.Membership = "Pro";

        // Assert
        Assert.AreEqual("user@example.com", response.Email);
        Assert.AreEqual("Pro", response.Membership);
    }
}
