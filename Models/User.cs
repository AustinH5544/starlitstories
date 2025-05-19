namespace Hackathon_2025.Models;

public class User
{
    public List<Story> Stories { get; set; } = new();
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Membership { get; set; } = "free";
    public int BooksGenerated { get; set; } = 0;
    public DateTime LastReset { get; set; } = DateTime.UtcNow;
}