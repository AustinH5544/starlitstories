//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using Hackathon_2025.Controllers;
//using Hackathon_2025.Models;
//using Hackathon_2025.Models.Auth;
//using Hackathon_2025.Data;
//using Hackathon_2025.Services;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.AspNetCore.Mvc;
//using Moq;
//using System.Threading.Tasks;
//using System;
//using Microsoft.Extensions.Configuration;

//namespace Hackathon_2025.Tests.Controllers;

//[TestClass]
//public class AuthControllerTests
//{
//    private AppDbContext _context = null!;
//    private Mock<IPasswordHasher<User>> _mockHasher = null!;
//    private Mock<IConfiguration> _mockConfig = null!;
//    private Mock<IEmailService> _mockEmail = null!;
//    private AuthController _controller = null!;

//    [TestInitialize]
//    public void TestInitialize()
//    {
//        var options = new DbContextOptionsBuilder<AppDbContext>()
//            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
//            .Options;

//        _context = new AppDbContext(options);
//        _mockHasher = new Mock<IPasswordHasher<User>>();
//        _mockConfig = new Mock<IConfiguration>();
//        _mockEmail = new Mock<IEmailService>();

//        // Setup minimal JWT config
//        _mockConfig.Setup(c => c["Jwt:Key"]).Returns("super_secret_key_123456");
//        _mockConfig.Setup(c => c["Jwt:Issuer"]).Returns("test_issuer");
//        _mockConfig.Setup(c => c["Jwt:Audience"]).Returns("test_audience");
//        _mockConfig.Setup(c => c["Jwt:ExpiresInMinutes"]).Returns("30");

//        _controller = new AuthController(_context, _mockHasher.Object, _mockConfig.Object, _mockEmail.Object);
//    }

//    [TestMethod]
//    public async Task Signup_ValidRequest_CreatesUserAndSendsVerificationEmail()
//    {
//        // Arrange
//        var request = new SignupRequest
//        {
//            Email = "test@example.com",
//            Username = "tester",
//            Password = "Password123",
//            Membership = "premium"
//        };
//        _mockHasher.Setup(h => h.HashPassword(It.IsAny<User>(), request.Password)).Returns("hashed_pw");

//        // Act
//        var result = await _controller.Signup(request) as OkObjectResult;

//        // Assert
//        Assert.IsNotNull(result);
//        dynamic response = result.Value!;
//        Assert.AreEqual("test@example.com", (string)response.email);
//        Assert.AreEqual("tester", (string)response.username);
//        Assert.IsTrue((bool)response.requiresVerification);
//        _mockEmail.Verify(e => e.SendVerificationEmailAsync("test@example.com", It.IsAny<string>()), Times.Once);
//    }

//    [TestMethod]
//    public async Task Login_InvalidPassword_ReturnsUnauthorized()
//    {
//        // Arrange
//        var user = new User
//        {
//            Email = "fail@example.com",
//            Username = "failuser",
//            UsernameNormalized = "failuser",
//            Membership = "free",
//            PasswordHash = "hashed_pw",
//            IsEmailVerified = true
//        };
//        _context.Users.Add(user);
//        await _context.SaveChangesAsync();

//        var request = new LoginRequest
//        {
//            Identifier = "fail@example.com",
//            Password = "wrong_password"
//        };
//        _mockHasher.Setup(h => h.VerifyHashedPassword(user, "hashed_pw", "wrong_password"))
//                   .Returns(PasswordVerificationResult.Failed);

//        // Act
//        var result = await _controller.Login(request);

//        // Assert
//        Assert.IsInstanceOfType(result, typeof(UnauthorizedObjectResult));
//        var unauthorized = result as UnauthorizedObjectResult;
//        Assert.AreEqual("Username/email or password is incorrect.", unauthorized?.Value);
//    }

//    [TestMethod]
//    public async Task ForgotPassword_ValidEmail_SendsResetEmail()
//    {
//        // Arrange
//        var user = new User { Email = "resetme@example.com" };
//        _context.Users.Add(user);
//        await _context.SaveChangesAsync();

//        var request = new ForgotPasswordRequest { Email = "resetme@example.com" };

//        // Act
//        var result = await _controller.ForgotPassword(request) as OkObjectResult;

//        // Assert
//        Assert.IsNotNull(result);
//        _mockEmail.Verify(e => e.SendPasswordResetEmailAsync("resetme@example.com", It.IsAny<string>()), Times.Once);
//    }

//    [TestMethod]
//    public async Task ResetPassword_ValidToken_UpdatesPassword()
//    {
//        // Arrange
//        var user = new User
//        {
//            Email = "user@example.com",
//            PasswordResetToken = "validtoken",
//            PasswordResetExpires = DateTime.UtcNow.AddMinutes(30)
//        };
//        _context.Users.Add(user);
//        await _context.SaveChangesAsync();

//        var request = new ResetPasswordRequest
//        {
//            Email = "user@example.com",
//            Token = "validtoken",
//            NewPassword = "NewPass123"
//        };
//        _mockHasher.Setup(h => h.HashPassword(user, request.NewPassword)).Returns("new_hashed_pw");

//        // Act
//        var result = await _controller.ResetPassword(request) as OkObjectResult;

//        // Assert
//        Assert.IsNotNull(result);
//        var updatedUser = await _context.Users.FirstAsync(u => u.Email == user.Email);
//        Assert.AreEqual("new_hashed_pw", updatedUser.PasswordHash);
//        Assert.IsNull(updatedUser.PasswordResetToken);
//        Assert.IsNull(updatedUser.PasswordResetExpires);
//    }
//}

