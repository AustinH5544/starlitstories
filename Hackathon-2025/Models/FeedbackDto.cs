namespace Hackathon_2025.Models;

public sealed class FeedbackDto
{
    public string? StoryId { get; set; }
    public string? StoryTitle { get; set; }
    public int? PageCount { get; set; }
    public int? EstReadMin { get; set; }
    public int? Enjoyment { get; set; }
    public string? PromptMatch { get; set; }
    public string? CharactersMatch { get; set; }
    public string? PromptCharactersOff { get; set; }
    public string? IllustrationsSatisfaction { get; set; }
    public string? IllustrationsOff { get; set; }
    public string? Navigation { get; set; }
    public int? ActualReadMin { get; set; }
    public string? StoryFlow { get; set; }
    public string? EncounteredBugs { get; set; }
    public string? Bugs { get; set; }
    public string? Performance { get; set; }
    public string? Likes { get; set; }
    public string? Improvements { get; set; }
    public string? NextFeature { get; set; }
    public string? ReadWith { get; set; }
    public string? CreateAgainLikelihood { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
}
