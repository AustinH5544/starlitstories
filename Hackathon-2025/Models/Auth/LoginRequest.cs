namespace Hackathon_2025.Models.Auth;

public class LoginRequest
{
    // Identifier can be email OR username
    public string Identifier { get; set; } = "";
    public string Password { get; set; } = "";
}