using System.ComponentModel.DataAnnotations;

namespace Hackathon_2025.Models;

public sealed record CheckoutRequest
{
    [Required]
    public MembershipPlan Membership { get; init; }
}
