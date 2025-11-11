using Hackathon_2025.Models.Auth;
using Hackathon_2025.Tests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Net;
using System.Net.Http.Json;

namespace Hackathon_2025.Tests.Controllers;

file sealed record SignupResponse(string message, string email, string username, bool requiresVerification);

[TestClass]
public class AuthControllerTests
{
    private TestWebAppFactory _factory = null!;
    private HttpClient _client = null!;

    // Represents the expected JSON returned from /api/auth/signup
    private sealed record SignupResponse(
        string message,
        string email,
        string username,
        bool requiresVerification
    );

    [TestInitialize]
    public void Init()
    {
        _factory = new TestWebAppFactory();
        _client = _factory.CreateClient();
    }

    [TestMethod]
    public async Task Signup_Returns_RequiresVerification_And_Sends_Email()
    {
        // Arrange
        var payload = new SignupRequest
        {
            Email = "test@x.com",
            Username = "tester",
            Password = "Pass123!"
        };

        // Act
        var resp = await _client.PostAsJsonAsync("/api/auth/signup", payload);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, "Expected 200 OK from /api/auth/signup");

        var json = await resp.Content.ReadFromJsonAsync<SignupResponse>();
        Assert.IsNotNull(json, "Response body should not be null");
        Assert.IsTrue(json!.requiresVerification, "Expected requiresVerification = true");

        _factory.EmailMock.Verify(
            e => e.SendVerificationEmailAsync("test@x.com", It.IsAny<string>()),
            Times.Once,
            "Expected SendVerificationEmailAsync to be called once");
    }
}
