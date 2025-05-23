using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;

namespace Hackathon2025.Tests.Models;

[TestClass]
public class AuthRequestTests
{
    [TestMethod]
    public void AuthRequest_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange
        AuthRequest request;

        // Act
        request = new AuthRequest();

        // Assert
        Assert.AreEqual(string.Empty, request.Email);
        Assert.AreEqual(string.Empty, request.Password);
        Assert.IsNull(request.Membership);
    }

    [TestMethod]
    public void AuthRequest_DefaultConstructor_SetsValuesCorrectly()
    {
        // Arrange
        AuthRequest request = new AuthRequest();

        // Act
        request.Email = "test@example.com";
        request.Password = "secure123";
        request.Membership = "Premium";

        // Assert
        Assert.AreEqual("test@example.com", request.Email);
        Assert.AreEqual("secure123", request.Password);
        Assert.AreEqual("Premium", request.Membership);
    }
}
