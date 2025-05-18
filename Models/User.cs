namespace Hackathon_2025.Models;

public class User
{
public int Id { get; set; }
public string Email { get; set; } = string.Empty;
public string PasswordHash { get; set; } = string.Empty;
public string Membership { get; set; } = "free";
}