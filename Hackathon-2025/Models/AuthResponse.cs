using System.ComponentModel.DataAnnotations;

namespace Hackathon_2025.Models;

public sealed record AuthResponse
{
    [EmailAddress] public required string Email { get; init; }
    public required MembershipPlan Membership { get; init; }
}
