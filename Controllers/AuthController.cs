using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Microsoft.AspNetCore.Identity;
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
}
