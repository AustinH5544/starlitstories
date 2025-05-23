namespace Hackathon_2025.Models;

public class StoryRequest
{
    public string Email { get; set; } = "";
    public string Theme { get; set; } = "";
    public List<CharacterSpec> Characters { get; set; } = new(); // Can include main, pet, etc.
}