namespace Hackathon_2025.Models;

public class StoryPage
{
    public int Id { get; set; }
    public string Text { get; set; }
    public string ImagePrompt { get; set; }
    public string? ImageUrl { get; set; }

    public int? StoryId { get; set; }
    public Story? Story { get; set; }
}
