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
        var payload = new SignupRequest
        {
            Email = "test@x.com",
            Username = "tester",
            Password = "Pass123!"
        };

        var resp = await _client.PostAsJsonAsync("/api/auth/signup", payload);

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
        // first signup
        var first = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = "dupe@x.com",
            Username = "duper",
            Password = "Pass123!"
        });
        Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);

        // second signup with same email
        var second = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = "dupe@x.com",
            Username = "duper2",
            Password = "Pass123!"
        });

        Assert.AreEqual(HttpStatusCode.BadRequest, second.StatusCode);
    }

    // =========================================================
    // Login
    // =========================================================
    [TestMethod]
    public async Task Login_Fails_When_Email_Not_Verified()
    {
        // Create unverified user
        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = "needverify@x.com",
            Username = "needverify",
            Password = "Pass123!"
        });
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);

        var resp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Identifier = "needverify@x.com",
            Password = "Pass123!"
        });

        Assert.AreEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
        var json = await ReadJson(resp);
        Assert.IsTrue(json.TryGetProperty("requiresVerification", out var rv) && rv.GetBoolean(),
            "Expected requiresVerification=true when logging in with unverified email");
    }

    [TestMethod]
    public async Task Login_Succeeds_For_Verified_User()
    {
        // Sign up
        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = "ok@x.com",
            Username = "ok",
            Password = "Pass123!"
        });
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);

        // Mark verified directly in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.SingleAsync(x => x.Email == "ok@x.com");
            u.IsEmailVerified = true;
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Identifier = "ok@x.com",
            Password = "Pass123!"
        });

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
        // Sign up to get a verification token
        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = "verifyme@x.com",
            Username = "verifyme",
            Password = "Pass123!"
        });
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);

        string token;
        int userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.SingleAsync(x => x.Email == "verifyme@x.com");
            Assert.IsFalse(u.IsEmailVerified, "User should start unverified");
            token = u.EmailVerificationToken!;
            userId = u.Id;
        }

        var verifyResp = await _client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest
        {
            Token = token
        });

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
        // signup triggers first email
        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = "again@x.com",
            Username = "again",
            Password = "Pass123!"
        });
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);

        // resend should trigger another email
        var resp = await _client.PostAsJsonAsync("/api/auth/resend-verification",
            new ResendVerificationRequest { Email = "again@x.com" });

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);

        _factory.EmailMock.Verify(
            e => e.SendVerificationEmailAsync("again@x.com", It.IsAny<string>()),
            Times.Exactly(2), // signup + resend
            "Expected verification email to be sent twice (signup + resend)");
    }

    [TestMethod]
    public async Task ResendVerification_Returns_BadRequest_If_Already_Verified()
    {
        // signup
        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = "already@x.com",
            Username = "already",
            Password = "Pass123!"
        });
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);

        // mark verified
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.SingleAsync(x => x.Email == "already@x.com");
            u.IsEmailVerified = true;
            await db.SaveChangesAsync();
        }

        var resp = await _client.PostAsJsonAsync("/api/auth/resend-verification",
            new ResendVerificationRequest { Email = "already@x.com" });

        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // =========================================================
    // Forgot / Reset Password
    // =========================================================
    [TestMethod]
    public async Task ForgotPassword_Is_Always_200_And_Sends_When_User_Exists()
    {
        // create a user
        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = "pw@x.com",
            Username = "pw",
            Password = "Pass123!"
        });
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);

        var resp = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = "pw@x.com" });

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);

        _factory.EmailMock.Verify(
            e => e.SendPasswordResetEmailAsync("pw@x.com", It.IsAny<string>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ResetPassword_Succeeds_With_Valid_Token_And_Allows_Login_With_New_Password()
    {
        const string email = "reset@x.com";
        const string oldPwd = "Pass123!";
        const string newPwd = "NewPass123!";

        // signup
        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = email,
            Username = "resetuser",
            Password = oldPwd
        });
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);

        // request reset -> sets token & expiry
        var forgot = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });
        Assert.AreEqual(HttpStatusCode.OK, forgot.StatusCode);

        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.SingleAsync(x => x.Email == email);
            token = u.PasswordResetToken!;
            u.IsEmailVerified = true; // ensure login works after reset
            await db.SaveChangesAsync();
        }

        // reset with valid token
        var reset = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { Email = email, Token = token, NewPassword = newPwd });

        Assert.AreEqual(HttpStatusCode.OK, reset.StatusCode);

        // login with new password
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Identifier = email, Password = newPwd });

        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        var json = await ReadJson(login);
        Assert.IsTrue(json.TryGetProperty("token", out _));
    }

    [TestMethod]
    public async Task ResetPassword_Fails_With_Invalid_Token()
    {
        const string email = "badreset@x.com";

        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = email,
            Username = "badreset",
            Password = "Pass123!"
        });
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);

        // trigger forgot to set a real token (we'll ignore it)
        var forgot = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });
        Assert.AreEqual(HttpStatusCode.OK, forgot.StatusCode);

        var reset = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { Email = email, Token = "not-the-right-token", NewPassword = "NewPass123!" });

        Assert.AreEqual(HttpStatusCode.BadRequest, reset.StatusCode);
    }

    [TestMethod]
    public async Task ResetPassword_Fails_With_Weak_Password()
    {
        const string email = "weak@x.com";

        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest
        {
            Email = email,
            Username = "weak",
            Password = "Pass123!"
        });
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);

        // set a valid token via forgot
        var forgot = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });
        Assert.AreEqual(HttpStatusCode.OK, forgot.StatusCode);

        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            token = (await db.Users.SingleAsync(x => x.Email == email)).PasswordResetToken!;
        }

        // weak password (no digits or too short etc.)
        var reset = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { Email = email, Token = token, NewPassword = "short" });

        Assert.AreEqual(HttpStatusCode.BadRequest, reset.StatusCode);
    }
}
