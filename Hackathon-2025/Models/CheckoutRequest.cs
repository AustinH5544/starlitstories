namespace Hackathon_2025.Models;

public sealed record CheckoutRequest
{
    public required MembershipPlan Membership { get; init; }
}