namespace Hackathon_2025.Models.Auth;

public sealed record LoginRequest
{
    // email OR username
    [System.ComponentModel.DataAnnotations.StringLength(256)]
    public required string Identifier { get; init; }

    [System.ComponentModel.DataAnnotations.StringLength(256, MinimumLength = 8)]
    public required string Password { get; init; }
}