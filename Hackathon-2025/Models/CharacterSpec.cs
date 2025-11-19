namespace Hackathon_2025.Models;

public sealed record CharacterSpec
{
    /// <summary>
    /// Free-form role label (e.g., "Main", "Mom", "Dad", "Pet", "Teacher", etc.).
    /// </summary>
    public string Role { get; init; } = "Main";

    public required string Name { get; init; }

    public bool IsAnimal { get; init; }

    public Dictionary<string, string> DescriptionFields { get; init; }
        = new(StringComparer.OrdinalIgnoreCase);
}
