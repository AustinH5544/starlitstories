using System.ComponentModel.DataAnnotations;

namespace Hackathon_2025.Models;

public sealed record AuthRequest
{
    [EmailAddress, StringLength(256)]
    public required string Email { get; init; }

    [StringLength(256, MinimumLength = 8)]
    public required string Password { get; init; }

    public MembershipPlan? Membership { get; init; }
}
