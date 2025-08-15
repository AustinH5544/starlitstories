using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Hackathon_2025.Models;

namespace Hackathon_2025.Services;

public static class PromptBuilder
{
    private static string ArtStyle =>
    "Children's book illustration in a consistent watercolor art style. Soft lighting. Gentle pastel tones. No outlines. Hand-painted look. Full-body character. Centered. Front-facing. Flat background. No text. No logos. No UI. No physical books. No visual aids. No color palettes. No swatches. No design guides. Only illustrate the scene. The characters must appear visually consistent in every image.";

    public static string BuildImagePrompt(List<CharacterSpec> characters, string paragraph)
    {
        string anchors = string.Join(" ", characters.Select(GetCharacterAnchor));
        string scene = SummarizeScene(paragraph);
        return $"{ArtStyle} {anchors} are {scene}.";
    }

    /// <summary>
    /// Image prompt that considers reading level (no reader age).
    /// </summary>
    public static string BuildImagePrompt(
        List<CharacterSpec> characters,
        string paragraph,
        string? readingLevel)
    {
        string anchors = string.Join(" ", characters.Select(GetCharacterAnchor));
        string scene = SummarizeScene(paragraph);

        var (visualMood, tone) = GetReadingProfile(readingLevel);

        // We only influence the style/mood—no character names/appearance beyond anchors.
        return $"{ArtStyle} Use {visualMood}. Keep the tone {tone}. {anchors} are {scene}.";
    }

    private static string GetCharacterAnchor(CharacterSpec character)
    {
        var description = character.IsAnimal
            ? BuildAnimalDescription(character.DescriptionFields)
            : BuildHumanDescription(character.DescriptionFields);

        return $"A consistent character named {character.Name}, who is {description}. (Role: {character.Role})";
    }

    private static string BuildHumanDescription(Dictionary<string, string> fields)
    {
        var parts = new List<string>();

        if (fields.TryGetValue("age", out var age))
            parts.Add($"a {age}-year-old");

        if (fields.TryGetValue("gender", out var gender))
            parts.Add(gender);

        if (fields.TryGetValue("skinTone", out var skin))
            parts.Add($"with {skin} skin");

        if (fields.TryGetValue("hairColor", out var hair))
        {
            if (fields.TryGetValue("hairStyle", out var style))
                parts.Add($"{style} {hair} hair");
            else
                parts.Add($"{hair} hair");
        }

        if (fields.TryGetValue("eyeColor", out var eyes))
            parts.Add($"{eyes} eyes");

        if (fields.TryGetValue("shirtColor", out var shirt))
            parts.Add($"wearing a {shirt} shirt");

        if (fields.TryGetValue("pantsColor", out var pants))
            parts.Add($"and {pants} pants");

        if (fields.TryGetValue("shoeColor", out var shoes))
            parts.Add($"with {shoes} shoes");

        if (fields.TryGetValue("accessory", out var accessory) && !string.IsNullOrWhiteSpace(accessory))
            parts.Add($"with a {accessory}");

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Maps readingLevel to guidance we can inject into prompts.
    /// </summary>
    private static (string VisualMood, string Tone) GetReadingProfile(string? readingLevel)
    {
        // Defaults (covers null/unknown)
        string visualMood = "gentle, friendly, inviting visuals";
        string tone = "warm, comforting, imaginative";

        switch ((readingLevel ?? "").Trim().ToLowerInvariant())
        {
            case "pre":
                visualMood = "very soft, simple, friendly visuals with clear shapes";
                tone = "soothing, rhythmic, very simple concepts";
                break;
            case "early":
                visualMood = "bright, engaging visuals with clear actions and expressions";
                tone = "short, clear, age-appropriate language";
                break;
            case "independent":
                visualMood = "slightly more detailed, adventurous visuals";
                tone = "engaging, varied, age-appropriate language";
                break;
            default:
                // leave defaults
                break;
        }

        return (visualMood, tone);
    }

    private static string BuildAnimalDescription(Dictionary<string, string> fields)
    {
        var species = fields.TryGetValue("species", out var s) ? s : "animal";
        string color = "";
        if (fields.TryGetValue("bodyColor", out var bodyColor) && fields.TryGetValue("bodyCovering", out var covering))
        {
            color = $"with {bodyColor} {covering}";
        }
        var accessory = fields.TryGetValue("accessory", out var a) ? $"wearing {a}" : "";

        return $"a {species} {color} {accessory}".Trim();
    }

    private static string SummarizeScene(string paragraph)
    {
        paragraph = paragraph.ToLower();

        if (paragraph.Contains("owl")) return "talking to a wise owl in a glowing forest";
        if (paragraph.Contains("fireflies")) return "walking through a grove of glowing fireflies";
        if (paragraph.Contains("riddle")) return "solving a riddle under a large tree";
        if (paragraph.Contains("clearing")) return "standing in a forest clearing";
        if (paragraph.Contains("path")) return "walking down a winding forest path";
        if (paragraph.Contains("glow")) return "surrounded by magical glowing plants";

        return "exploring a magical forest";
    }

    public static async Task<string> BuildImagePromptWithSceneAsync(
        List<CharacterSpec> characters,
        string paragraph,
        HttpClient httpClient,
        string apiKey)
    {
        string anchors = string.Join(" ", characters.Select(GetCharacterAnchor));

        var userPrompt = $"""
        Summarize the following story paragraph into a short visual scene that can be used in an illustration. 
        Describe only what the characters are doing and what the environment looks like. 
        Do not mention names, appearance, or clothing.

        Paragraph: "{paragraph}"
        """;

        var requestBody = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "You are an assistant generating image prompts for a children's storybook. Never describe the characters' names, appearance, or clothing. Only describe the environment and actions."
                },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.3,
            max_tokens = 60
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var res = await httpClient.SendAsync(req);
        res.EnsureSuccessStatusCode();

        var json = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());
        var scene = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

        return $"{ArtStyle} {anchors} are {scene}.";
    }

    public static string BuildCoverPrompt(List<CharacterSpec> characters, string theme)
    {
        string anchors = string.Join(" ", characters.Select(GetCharacterAnchor));
        return $"""
        Children's book watercolor illustration in a consistent art style. Soft lighting. Gentle pastel tones. No outlines. Hand-painted look. Full-body characters. Centered. Front-facing. Flat background. No text. No logos. No UI elements. No titles. No visual aids. No swatches. No book elements. Do not include any design features. Only illustrate the scene.

        Depict: {anchors}, in a setting inspired by the theme: {theme}.
        """;
    }

    /// <summary>
    /// Cover prompt that considers reading level (no reader age).
    /// </summary>
    public static string BuildCoverPrompt(
        List<CharacterSpec> characters,
        string theme,
        string? readingLevel)
    {
        string anchors = string.Join(" ", characters.Select(GetCharacterAnchor));
        var (visualMood, tone) = GetReadingProfile(readingLevel);

        return $"""
    Children's book watercolor illustration in a consistent style.
    Use {visualMood}. Keep the tone {tone}.
    Show only the scene; no text, no logos, no book design elements.
    Depict: {anchors}, in a setting inspired by the theme: {theme}.
    """;
    }
}
