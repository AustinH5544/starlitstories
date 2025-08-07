namespace Hackathon_2025.Models;

public class Story
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string CoverImageUrl { get; set; } = "";

    public List<StoryPage> Pages { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign Key
    public int UserId { get; set; }
    public User? User { get; set; }
}
