using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Hackathon_2025.Models.Auth;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hackathon_2025.Tests.Controllers;

// local DTO for the signup response only (not reused elsewhere)
file sealed record SignupResponse(string message, string email, string username, bool requiresVerification);

[TestClass]
public class AuthControllerTests
{
    private TestWebAppFactory _factory = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Init()
    {
        // Arrange (common): spin up test server and client
        _factory = new TestWebAppFactory();
        _client = _factory.CreateClient();
    }

    // -------------------------
    // Helpers
    // -------------------------
    private AppDbContext GetDb()
        => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private static async Task<JsonElement> ReadJson(HttpResponseMessage resp)
    {
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.IsNotNull(doc, "Response JSON was null");
        return doc!.RootElement.Clone();
    }

    // =========================================================
    // Signup
    // =========================================================
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
            Times.Once);
    }

    [TestMethod]
    public async Task Signup_Fails_When_Email_Already_Exists()
    {
        // Arrange
        var firstPayload = new SignupRequest
        {
            Email = "dupe@x.com",
            Username = "duper",
            Password = "Pass123!"
        };

        var secondPayload = new SignupRequest
        {
            Email = "dupe@x.com",
            Username = "duper2",
            Password = "Pass123!"
        };

        // Act
        var first = await _client.PostAsJsonAsync("/api/auth/signup", firstPayload);
        var second = await _client.PostAsJsonAsync("/api/auth/signup", secondPayload);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, second.StatusCode);
    }

    // =========================================================
    // Login
    // =========================================================
    [TestMethod]
    public async Task Login_Fails_When_Email_Not_Verified()
    {
        // Arrange
        var signupPayload = new SignupRequest
        {
            Email = "needverify@x.com",
            Username = "needverify",
            Password = "Pass123!"
        };

        // Act
        var signup = await _client.PostAsJsonAsync("/api/auth/signup", signupPayload);
        var resp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Identifier = "needverify@x.com",
            Password = "Pass123!"
        });

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, resp.StatusCode);

        var json = await ReadJson(resp);
        Assert.IsTrue(
            json.TryGetProperty("requiresVerification", out var rv) && rv.GetBoolean(),
            "Expected requiresVerification=true when logging in with unverified email");
    }

    [TestMethod]
    public async Task Login_Succeeds_For_Verified_User()
    {
        // Arrange
        const string email = "ok@x.com";
        const string username = "ok_user";
        const string password = "Pass123!";

        // Act
        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = email,
            Username = username,
            Password = password
        });

        // mark verified directly in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.SingleAsync(x => x.Email == email);
            u.IsEmailVerified = true;
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Identifier = email,
            Password = password
        });

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);

        var json = await ReadJson(resp);
        Assert.IsTrue(json.TryGetProperty("token", out _), "Expected JWT token on successful login");
    }

    // =========================================================
    // Verify Email
    // =========================================================
    [TestMethod]
    public async Task VerifyEmail_Sets_IsVerified_And_Returns_Token()
    {
        // Arrange
        const string email = "verifyme@x.com";

        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = email,
            Username = "verifyme",
            Password = "Pass123!"
        });

        string token;
        int userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.SingleAsync(x => x.Email == email);
            Assert.IsFalse(u.IsEmailVerified, "User should start unverified");
            token = u.EmailVerificationToken!;
            userId = u.Id;
        }

        // Act
        var verifyResp = await _client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest
        {
            Token = token
        });

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, verifyResp.StatusCode);

        var json = await ReadJson(verifyResp);
        Assert.IsTrue(json.TryGetProperty("token", out _), "Expected token in verify-email response");

        // Confirm persisted
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.FindAsync(userId);
            Assert.IsNotNull(u);
            Assert.IsTrue(u!.IsEmailVerified, "User should be marked verified after verify-email");
            Assert.IsNull(u.EmailVerificationToken);
            Assert.IsNull(u.EmailVerificationExpires);
        }
    }

    // =========================================================
    // Resend Verification
    // =========================================================
    [TestMethod]
    public async Task ResendVerification_Sends_Email_For_Unverified_User()
    {
        // Arrange
        const string email = "again@x.com";

        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = email,
            Username = "again",
            Password = "Pass123!"
        });

        // Act
        var resp = await _client.PostAsJsonAsync("/api/auth/resend-verification",
            new ResendVerificationRequest { Email = email });

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);

        _factory.EmailMock.Verify(
            e => e.SendVerificationEmailAsync(email, It.IsAny<string>()),
            Times.Exactly(2), // signup + resend
            "Expected verification email to be sent twice (signup + resend)");
    }

    [TestMethod]
    public async Task ResendVerification_Returns_BadRequest_If_Already_Verified()
    {
        // Arrange
        const string email = "already@x.com";

        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = email,
            Username = "already",
            Password = "Pass123!"
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.SingleAsync(x => x.Email == email);
            u.IsEmailVerified = true;
            await db.SaveChangesAsync();
        }

        // Act
        var resp = await _client.PostAsJsonAsync("/api/auth/resend-verification",
            new ResendVerificationRequest { Email = email });

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // =========================================================
    // Forgot / Reset Password
    // =========================================================
    [TestMethod]
    public async Task ForgotPassword_Is_Always_200_And_Sends_When_User_Exists()
    {
        // Arrange
        const string email = "pw@x.com";

        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = email,
            Username = "pw_user",
            Password = "Pass123!"
        });

        // Act
        var resp = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);

        _factory.EmailMock.Verify(
            e => e.SendPasswordResetEmailAsync(email, It.IsAny<string>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ResetPassword_Succeeds_With_Valid_Token_And_Allows_Login_With_New_Password()
    {
        // Arrange
        const string email = "reset@x.com";
        const string oldPwd = "Pass123!";
        const string newPwd = "NewPass123!";

        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = email,
            Username = "resetuser",
            Password = oldPwd
        });

        var forgot = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });

        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.SingleAsync(x => x.Email == email);
            token = u.PasswordResetToken!;
            u.IsEmailVerified = true; // ensure login works after reset
            await db.SaveChangesAsync();
        }

        // Act
        var reset = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { Email = email, Token = token, NewPassword = newPwd });

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Identifier = email, Password = newPwd });

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, forgot.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, reset.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);

        var json = await ReadJson(login);
        Assert.IsTrue(json.TryGetProperty("token", out _));
    }

    [TestMethod]
    public async Task ResetPassword_Fails_With_Invalid_Token()
    {
        // Arrange
        const string email = "badreset@x.com";

        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = email,
            Username = "badreset",
            Password = "Pass123!"
        });

        var forgot = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });

        // Act
        var reset = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { Email = email, Token = "not-the-right-token", NewPassword = "NewPass123!" });

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, forgot.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, reset.StatusCode);
    }

    [TestMethod]
    public async Task ResetPassword_Fails_With_Weak_Password()
    {
        // Arrange
        const string email = "weak@x.com";

        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = email,
            Username = "weak",
            Password = "Pass123!"
        });

        var forgot = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });

        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            token = (await db.Users.SingleAsync(x => x.Email == email)).PasswordResetToken!;
        }

        // Act
        var reset = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { Email = email, Token = token, NewPassword = "short" });

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, forgot.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, reset.StatusCode);
    }
}
