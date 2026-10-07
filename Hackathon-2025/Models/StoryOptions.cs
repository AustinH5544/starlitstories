namespace Hackathon_2025.Models;

public sealed class StoryOptions
{
    /// <summary>
    /// If true, the server ENFORCES length hints (short/medium/long).
    /// If false, the server IGNORES length hints and lets the model decide.
    /// </summary>
    public bool LengthHintEnabled { get; set; } = false;

    /// <summary>
    /// If true, the floating progress pill (page X of Y) is shown in the story viewer.
    /// Set to false to hide it globally.
    /// </summary>
    public bool ShowProgressPill { get; set; } = false;
}