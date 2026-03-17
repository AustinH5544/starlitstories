using System.ComponentModel.DataAnnotations;

namespace Hackathon_2025.Models.Auth;

public sealed record SignupRequest
{
    [System.ComponentModel.DataAnnotations.EmailAddress, StringLength(256)]
    public required string Email { get; init; }

    [System.ComponentModel.DataAnnotations.RegularExpression(Hackathon_2025.Services.UsernameRules.Pattern)]
    public required string Username { get; init; }

    [StringLength(256, MinimumLength = 8)]
    public required string Password { get; init; }

    public MembershipPlan? Membership { get; init; }

    [StringLength(2048)]
    public string? TurnstileToken { get; init; }
}
