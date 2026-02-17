using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hackathon_2025.Models;

public class StoryPage
{
    public int Id { get; set; }

    [Required, MaxLength(4000)]
    public string Text { get; set; } = null!;

    [Required, MaxLength(2000)]
    public string ImagePrompt { get; set; } = null!;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public int StoryId { get; set; }
    public Story Story { get; set; } = null!;

    // Prefer EF to use this; keep it protected
    protected StoryPage() { }

    public StoryPage(string text, string imagePrompt)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        ImagePrompt = imagePrompt ?? throw new ArgumentNullException(nameof(imagePrompt));
    }
}