using System.ComponentModel.DataAnnotations;

namespace Hackathon_2025.Models;

public class Story
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    // Frequently absent until images are generated → make it nullable
    [MaxLength(500)]
    public string? CoverImageUrl { get; set; }

    public ICollection<StoryPage> Pages { get; set; } = new List<StoryPage>();
    public ICollection<StoryShare> Shares { get; set; } = new List<StoryShare>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // required FK + nav
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
