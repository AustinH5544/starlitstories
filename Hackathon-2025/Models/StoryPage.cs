using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hackathon_2025.Models;

public class StoryPage
{
    public int Id { get; set; }

    [Required]
    public string Text { get; set; }

    [Required]
    public string ImagePrompt { get; set; }
    public string? ImageUrl { get; set; }

    [ForeignKey("StoryId")]
    public int? StoryId { get; set; }
    public Story? Story { get; set; }

    // Constructor to enforce required properties
    public StoryPage(string text, string imagePrompt)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        ImagePrompt = imagePrompt ?? throw new ArgumentNullException(nameof(imagePrompt));
    }

    // Parameterless constructor for EF Core
    public StoryPage() { }
}
