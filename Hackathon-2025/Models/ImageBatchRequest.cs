namespace Hackathon_2025.Models;

public sealed record ImageBatchRequest
{
    [System.ComponentModel.DataAnnotations.MinLength(1)]
    public required List<string> Prompts { get; init; }
}
