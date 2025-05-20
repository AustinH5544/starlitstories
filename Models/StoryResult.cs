namespace Hackathon_2025.Models;

public class StoryResult
{
    public string Title { get; set; } = "";
    public string CoverImagePrompt { get; set; } = "";
    public string CoverImageUrl { get; set; } = "";
    public List<StoryPage> Pages { get; set; } = new();
}
