namespace Hackathon_2025.Models;

public class StoryShare
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public Story Story { get; set; } = null!;
    public string Token { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresUtc { get; set; }  // optional
    public DateTime? RevokedUtc { get; set; }  // optional
}