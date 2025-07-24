using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public AuthController(AppDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup(AuthRequest request)
    {
        if (_db.Users.Any(u => u.Email == request.Email))
            return BadRequest("Email already in use.");

        var user = new User
        {
            Email = request.Email,
            Membership = request.Membership ?? "free"
        };

        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new AuthResponse { Email = user.Email, Membership = user.Membership });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(AuthRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
            return Unauthorized("User not found.");

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result != PasswordVerificationResult.Success)
            return Unauthorized("Invalid password.");

        return Ok(new AuthResponse { Email = user.Email, Membership = user.Membership });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(Models.ForgotPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        // Always return success to prevent email enumeration attacks
        // In a real app, send an email with a reset token here

        if (user != null)
        {
            // Generate a password reset token (in production, store this in DB with expiration)
            var resetToken = Guid.NewGuid().ToString();
            user.PasswordResetToken = resetToken;
            user.PasswordResetExpires = DateTime.UtcNow.AddHours(1); // Token expires in 1 hour

            await _db.SaveChangesAsync();

            // In production, send email with reset link:
            // var resetLink = $"https://yourapp.com/reset-password?token={resetToken}&email={user.Email}";
            // await _emailService.SendPasswordResetEmail(user.Email, resetLink);

            // For demo purposes, we'll just log the token (remove in production!)
            Console.WriteLine($"Password reset token for {user.Email}: {resetToken}");
        }

        return Ok(new { message = "If an account with that email exists, we've sent a password reset link." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(Models.ResetPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email == request.Email &&
            u.PasswordResetToken == request.Token &&
            u.PasswordResetExpires > DateTime.UtcNow);

        if (user == null)
            return BadRequest("Invalid or expired reset token.");

        // Update password
        user.PasswordHash = _hasher.HashPassword(user, request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetExpires = null;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Password has been reset successfully." });
    }
}
