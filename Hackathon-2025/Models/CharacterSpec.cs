namespace Hackathon_2025.Models;

public class CharacterSpec
{
    public string Role { get; set; } = "main"; // e.g. "main", "friend", "guardian"
    public string Name { get; set; } = ""; // e.g. "Luna", "Max"
    public bool IsAnimal { get; set; } // True if animal character
                                       // Dictionary of customizable fields
    public Dictionary<string, string> DescriptionFields { get; set; } = new();
}