using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Controllers;
using Hackathon_2025.Models;
using Hackathon_2025.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Threading.Tasks;
using System;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class AuthControllerTests
{
    private AppDbContext _context = null!;
    private Mock<IPasswordHasher<User>> _mockHasher = null!;
    private AuthController _controller = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB per test
            .Options;

        _context = new AppDbContext(options);
        _mockHasher = new Mock<IPasswordHasher<User>>();
        _controller = new AuthController(_context, _mockHasher.Object);
    }

    [TestMethod]
    public async Task Signup_ValidRequest_CreatesUserAndReturnsOk()
    {
        // Arrange
        var request = new AuthRequest
        {
            Email = "test@example.com",
            Password = "MyPassword123",
            Membership = "premium"
        };
        _mockHasher.Setup(h => h.HashPassword(It.IsAny<User>(), request.Password)).Returns("hashed_pw");

        // Act
        var result = await _controller.Signup(request) as OkObjectResult;

        // Assert
        Assert.IsNotNull(result);
        var response = result.Value as AuthResponse;
        Assert.AreEqual(request.Email, response?.Email);
        Assert.AreEqual("premium", response?.Membership);
    }

    [TestMethod]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var user = new User
        {
            Email = "fail@example.com",
            Membership = "free",
            PasswordHash = "hashed_pw"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var request = new AuthRequest
        {
            Email = "fail@example.com",
            Password = "wrong_password"
        };
        _mockHasher.Setup(h => h.VerifyHashedPassword(user, "hashed_pw", "wrong_password"))
                   .Returns(PasswordVerificationResult.Failed);

        // Act
        var result = await _controller.Login(request) as UnauthorizedObjectResult;

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Invalid password.", result.Value);
    }

    [TestMethod]
    public async Task ForgotPassword_ValidEmail_SetsResetToken()
    {
        // Arrange
        var user = new User { Email = "resetme@example.com" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var request = new ForgotPasswordRequest { Email = "resetme@example.com" };

        // Act
        var result = await _controller.ForgotPassword(request) as OkObjectResult;

        // Assert
        Assert.IsNotNull(result);
        var updatedUser = await _context.Users.FirstAsync(u => u.Email == user.Email);
        Assert.IsNotNull(updatedUser.PasswordResetToken);
        Assert.IsTrue(updatedUser.PasswordResetExpires > DateTime.UtcNow);
    }

    [TestMethod]
    public async Task ResetPassword_ValidToken_UpdatesPassword()
    {
        // Arrange
        var user = new User
        {
            Email = "user@example.com",
            PasswordResetToken = "validtoken",
            PasswordResetExpires = DateTime.UtcNow.AddMinutes(30)
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var request = new ResetPasswordRequest
        {
            Email = "user@example.com",
            Token = "validtoken",
            NewPassword = "newSecurePass"
        };

        _mockHasher.Setup(h => h.HashPassword(user, request.NewPassword)).Returns("new_hashed");

        // Act
        var result = await _controller.ResetPassword(request) as OkObjectResult;

        // Assert
        Assert.IsNotNull(result);
        var updatedUser = await _context.Users.FirstAsync(u => u.Email == user.Email);
        Assert.AreEqual("new_hashed", updatedUser.PasswordHash);
        Assert.IsNull(updatedUser.PasswordResetToken);
        Assert.IsNull(updatedUser.PasswordResetExpires);
    }
}
