namespace Hackathon_2025.Models;

public sealed record StoryResult
{
    public required string Title { get; init; }
    public required string CoverImagePrompt { get; init; } // or omit if sensitive
    public string? CoverImageUrl { get; init; }
    public List<StoryPageDto> Pages { get; init; } = new();
}
