namespace Hackathon_2025.Models;

public sealed record VerifyEmailRequest
{
    public required string Token { get; init; }
}