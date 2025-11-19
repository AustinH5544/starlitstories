namespace Hackathon_2025.Models;

public sealed record CharacterSpec
{
    public CharacterRole Role { get; init; } = CharacterRole.Main;
    public required string Name { get; init; }
    public bool IsAnimal { get; init; }

    public Dictionary<string, string> DescriptionFields { get; init; }
        = new(StringComparer.OrdinalIgnoreCase);
}