using System.ComponentModel.DataAnnotations;

namespace Hackathon_2025.Models;

public sealed record StoryRequest
{
    public string? ReadingLevel { get; init; } // keep flexible
    public string? ArtStyle { get; init; }

    public required string Theme { get; init; }

    [MinLength(1)]
    public List<CharacterSpec> Characters { get; init; } = new();

    public int? PageCount { get; init; }
    public string? LessonLearned { get; init; }
    public string? StoryLength { get; init; }
}