using System.ComponentModel.DataAnnotations;

namespace Hackathon_2025.Models;

public sealed record ResetPasswordRequest
{
    [EmailAddress] public required string Email { get; init; }
    public required string Token { get; init; }
    [StringLength(256, MinimumLength = 8)]
    public required string NewPassword { get; init; }
}
