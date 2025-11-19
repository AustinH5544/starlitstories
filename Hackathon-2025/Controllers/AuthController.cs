using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Hackathon_2025.Models.Auth;
using Hackathon_2025.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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

    public AuthController(
        AppDbContext db,
        IPasswordHasher<User> hasher,
        IConfiguration config,
        IEmailService emailService)
    {
        _db = db;
        _hasher = hasher;
        _config = config;
        _emailService = emailService;
    }

    // Stronger default than before (8+ with letters and digits)
    private static bool IsPasswordValid(string p) =>
        !string.IsNullOrWhiteSpace(p) &&
        p.Length >= 8 &&
        p.Any(char.IsLetter) &&
        p.Any(char.IsDigit);

    [HttpPost("signup")]
    public async Task<IActionResult> Signup(SignupRequest request)
    {
        if (!IsPasswordValid(request.Password))
            return BadRequest(new { message = "Password must be at least 8 characters and include letters and numbers." });

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return BadRequest(new { message = "Email already in use." });

        var uname = request.Username.Trim();
        var unameNorm = uname.ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.UsernameNormalized == unameNorm))
            return BadRequest(new { message = "Username already in use." });

        var user = new User
        {
            Email = email,
            Username = uname,
            UsernameNormalized = unameNorm,

            // If client omitted membership, default to Free
            Membership = request.Membership ?? MembershipPlan.Free,

            EmailVerificationToken = Guid.NewGuid().ToString("N"),
            EmailVerificationExpires = DateTime.UtcNow.AddDays(1),
            IsEmailVerified = false
        };

        user.PasswordHash = _hasher.HashPassword(user, request.Password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await _emailService.SendVerificationEmailAsync(user.Email, user.EmailVerificationToken!);

        return Ok(new
        {
            message = "Account created successfully! Please check your email to verify your account.",
            email = user.Email,
            username = user.Username,
            requiresVerification = true
        });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.EmailVerificationToken == request.Token &&
            u.EmailVerificationExpires > DateTime.UtcNow);

        if (user is null)
            return BadRequest(new { message = "Invalid or expired verification token." });

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
            username = user.Username,
            membership = user.Membership.ToString()
        });
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Do not reveal existence
        if (user is null)
            return Ok(new { message = "If an account with that email exists, we've sent a verification email." });

        if (user.IsEmailVerified)
            return BadRequest(new { message = "Email is already verified." });

        user.EmailVerificationToken = Guid.NewGuid().ToString("N");
        user.EmailVerificationExpires = DateTime.UtcNow.AddDays(1);

        await _db.SaveChangesAsync();
        await _emailService.SendVerificationEmailAsync(user.Email, user.EmailVerificationToken!);

        return Ok(new { message = "Verification email sent successfully." });
    }

    [HttpPost("login")]
    [EnableRateLimiting("login-ip")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        User? user;

        if (request.Identifier.Contains('@'))
        {
            var email = request.Identifier.Trim().ToLowerInvariant();
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        }
        else
        {
            var unameNorm = request.Identifier.Trim().ToLowerInvariant();
            user = await _db.Users.FirstOrDefaultAsync(u => u.UsernameNormalized == unameNorm);
        }

        if (user is null)
            return Unauthorized("Username/email or password is incorrect.");

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result != PasswordVerificationResult.Success)
            return Unauthorized("Username/email or password is incorrect.");

        if (!user.IsEmailVerified)
            return Unauthorized(new
            {
                message = "Please verify your email before logging in.",
                requiresVerification = true,
                email = user.Email
            });

        var token = GenerateJwtToken(user);
        return Ok(new
        {
            token,
            email = user.Email,
            username = user.Username,
            membership = user.Membership.ToString()
        });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Always succeed to avoid user enumeration
        if (user is not null)
        {
            var resetToken = Guid.NewGuid().ToString("N");
            user.PasswordResetToken = resetToken;
            user.PasswordResetExpires = DateTime.UtcNow.AddHours(1);

            await _db.SaveChangesAsync();
            await _emailService.SendPasswordResetEmailAsync(user.Email, resetToken);
        }

        return Ok(new { message = "If an account with that email exists, we've sent a password reset link." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        if (!IsPasswordValid(request.NewPassword))
            return BadRequest(new { message = "Password must be at least 8 characters and include letters and numbers." });

        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email == email &&
            u.PasswordResetExpires > DateTime.UtcNow);

        if (user is null || !SlowEquals(user.PasswordResetToken, request.Token))
            return BadRequest(new { message = "Invalid or expired reset token." });

        user.PasswordHash = _hasher.HashPassword(user, request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetExpires = null;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Password has been reset successfully." });
    }

    private string GenerateJwtToken(User user)
    {
        var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("Missing Jwt:Key");
        var issuer = _config["Jwt:Issuer"];
        var audience = _config["Jwt:Audience"];
        var expiresInMinutes = Convert.ToDouble(_config["Jwt:ExpiresInMinutes"] ?? "60");

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("username", user.Username),
            new Claim("membership", user.Membership.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(expiresInMinutes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool SlowEquals(string? a, string? b)
    {
        if (a is null || b is null || a.Length != b.Length) return false;

        var diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];

        return diff == 0;
    }
}
