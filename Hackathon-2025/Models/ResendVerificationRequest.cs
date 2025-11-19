using System.ComponentModel.DataAnnotations;

namespace Hackathon_2025.Models;

public sealed record ResendVerificationRequest
{
    [EmailAddress] public required string Email { get; init; }
}
