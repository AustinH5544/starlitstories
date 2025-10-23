namespace Hackathon_2025.Models;

public sealed class FeedbackDto
{
    public string? StoryId { get; set; }
    public string? StoryTitle { get; set; }
    public int? PageCount { get; set; }
    public int? EstReadMin { get; set; }
    public int? Enjoyment { get; set; }
    public string? Personalization { get; set; }
    public string? Illustrations { get; set; }
    public string? Navigation { get; set; }
    public string? ReadTimeAccuracy { get; set; }
    public int? ActualReadMin { get; set; }
    public string? Bugs { get; set; }
    public string? Performance { get; set; }
    public string? Likes { get; set; }
    public string? Improvements { get; set; }
    public string? FutureInterest { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string[]? Notify { get; set; } // optional override from client
}