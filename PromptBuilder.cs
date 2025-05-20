using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

namespace Hackathon_2025.Services;

public static class PromptBuilder
{
    private static string ArtStyle =>
        "Children’s book illustration in a consistent watercolor art style. Soft lighting. Gentle pastel tones. No outlines. Hand-painted look. Full-body character. Centered. Front-facing. Flat background. No text. No logos. No UI. No physical books. No visual aids. No color palettes. No swatches. No design guides. Only illustrate the scene.";

    public static string BuildImagePrompt(string characterName, string characterDescription, string paragraph)
    {
        string anchor = GetCharacterAnchor(characterName, characterDescription);
        string scene = SummarizeScene(paragraph);
        return $"{ArtStyle} {anchor} is {scene}.";
    }

    private static string GetCharacterAnchor(string characterName, string characterDescription)
    {
        // Locked-in description format to reduce visual drift
        return $"A child named {characterName}, who has {characterDescription}";
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
        string characterName,
        string characterDescription,
        string paragraph,
        HttpClient httpClient,
        string apiKey)
    {
        string anchor = GetCharacterAnchor(characterName, characterDescription);

        var userPrompt = $"""
        Summarize the following story paragraph into a short visual scene that can be used in an illustration. 
        Describe only what the main character is doing and what the environment looks like. 
        Do not mention the character’s name or appearance — that will be added separately.

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
                    content = "You are an assistant generating image prompts for a children's storybook. Never describe the character’s name, appearance, or clothing. Only describe the environment and what the character is doing."
                },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.3, // Minimize randomness for consistency
            max_tokens = 60
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var res = await httpClient.SendAsync(req);
        res.EnsureSuccessStatusCode();

        var json = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());
        var scene = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

        return $"{ArtStyle} {anchor} is {scene}.";
    }

    public static string BuildCoverPrompt(string characterName, string characterDescription, string theme)
    {
        var anchor = GetCharacterAnchor(characterName, characterDescription);

        return $"""
    Children’s book watercolor illustration in a consistent art style. Soft lighting. Gentle pastel tones. No outlines. Hand-painted look. Full-body character. Centered. Front-facing. Flat background. No text. No logos. No UI elements. No titles. No visual aids. No color palettes. No swatches. No book elements. Do not include any design features. Only illustrate the scene.

    Depict: {anchor}, in a setting inspired by the theme: {theme}.
    """;
    }
}
