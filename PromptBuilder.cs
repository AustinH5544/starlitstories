namespace Hackathon_2025.Services;

public static class PromptBuilder
{
    public static string BuildImagePrompt(string characterName, string characterDescription, string paragraph)
    {
        string style = "Children’s book watercolor illustration. Soft lighting. Gentle pastel tones. Full body. Storybook style.";
        string anchor = $"{characterName}, {characterDescription}";

        string scene = SummarizeScene(paragraph);

        return $"{style} {anchor} is {scene}.";
    }

    private static string SummarizeScene(string paragraph)
    {
        // Rule-based fallback for common scenes. Later, upgrade with LLM if needed.
        paragraph = paragraph.ToLower();

        if (paragraph.Contains("owl")) return "talking to a wise owl in a glowing forest";
        if (paragraph.Contains("fireflies")) return "walking through a grove of glowing fireflies";
        if (paragraph.Contains("riddle")) return "solving a riddle under a large tree";
        if (paragraph.Contains("clearing")) return "standing in a forest clearing";
        if (paragraph.Contains("path")) return "walking down a winding forest path";
        if (paragraph.Contains("glow")) return "surrounded by magical glowing plants";

        return "exploring a magical forest";
    }
}
