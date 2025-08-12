using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly IConfiguration _config;
    private readonly IEmailService _emailService;

    public AuthController(AppDbContext db, IPasswordHasher<User> hasher, IConfiguration config, IEmailService emailService)
    {
        _db = db;
        _hasher = hasher;
        _config = config;
        _emailService = emailService;
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup(AuthRequest request)
    {
        if (_db.Users.Any(u => u.Email == request.Email))
            return BadRequest("Email already in use.");

        var user = new User
        {
            Email = request.Email,
            Membership = request.Membership ?? "free",
            EmailVerificationToken = Guid.NewGuid().ToString(),
            EmailVerificationExpires = DateTime.UtcNow.AddDays(1), // 24 hours to verify
            IsEmailVerified = false
        };

        user.PasswordHash = _hasher.HashPassword(user, request.Password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Send verification email
        await _emailService.SendVerificationEmailAsync(user.Email, user.EmailVerificationToken);

        return Ok(new
        {
            message = "Account created successfully! Please check your email to verify your account.",
            email = user.Email,
            requiresVerification = true
        });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email == request.Email &&
            u.EmailVerificationToken == request.Token &&
            u.EmailVerificationExpires > DateTime.UtcNow);

        if (user == null)
            return BadRequest("Invalid or expired verification token.");

        user.IsEmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationExpires = null;

        await _db.SaveChangesAsync();

        var token = GenerateJwtToken(user);
        return Ok(new
        {
            message = "Email verified successfully!",
            token,
            email = user.Email,
            membership = user.Membership
        });
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
            return Ok(new { message = "If an account with that email exists, we've sent a verification email." });

        if (user.IsEmailVerified)
            return BadRequest("Email is already verified.");

        // Generate new verification token
        user.EmailVerificationToken = Guid.NewGuid().ToString();
        user.EmailVerificationExpires = DateTime.UtcNow.AddDays(1);

        await _db.SaveChangesAsync();
        await _emailService.SendVerificationEmailAsync(user.Email, user.EmailVerificationToken);

        return Ok(new { message = "Verification email sent successfully." });
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

        if (!user.IsEmailVerified)
            return Unauthorized(new
            {
                message = "Please verify your email before logging in.",
                requiresVerification = true,
                email = user.Email
            });

        var token = GenerateJwtToken(user);
        return Ok(new { token, email = user.Email, membership = user.Membership });
    }

    private string GenerateJwtToken(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("membership", user.Membership ?? "free")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_config["Jwt:ExpiresInMinutes"]!));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(Models.ForgotPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        // Always return success to prevent email enumeration attacks
        if (user != null)
        {
            var resetToken = Guid.NewGuid().ToString();
            user.PasswordResetToken = resetToken;
            user.PasswordResetExpires = DateTime.UtcNow.AddHours(1);

            await _db.SaveChangesAsync();
            await _emailService.SendPasswordResetEmailAsync(user.Email, resetToken);
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

        user.PasswordHash = _hasher.HashPassword(user, request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetExpires = null;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Password has been reset successfully." });
    }
}
