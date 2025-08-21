namespace Hackathon_2025.Models;

public class StoryRequest
{
    public string? ReadingLevel { get; set; } // "pre" | "early" | "independent" (optional)
    public string? ArtStyle { get; set; }
    public string Theme { get; set; } = "";
    public List<CharacterSpec> Characters { get; set; } = new(); // Can include main, pet, etc.
    public int? PageCount { get; set; }
}