namespace Hackathon_2025.Models.Auth;

public class SignupRequest
{
    public string Email { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Membership { get; set; }
}
