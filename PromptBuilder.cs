using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

namespace Hackathon_2025.Services;

public static class PromptBuilder
{
    private static string ArtStyle =>
    "Watercolor illustration in the style of Beatrix Potter. Soft lighting. Gentle pastel tones. Full-body character composition. Front-facing, centered layout. Suitable for a children's storybook. Maintain this exact art style across all images. Do not include any text, letters, or writing. Do not draw or depict any physical book.";

    public static string BuildImagePrompt(string characterName, string characterDescription, string paragraph)
    {
        //string style = "Children’s book watercolor illustration. Soft lighting. Gentle pastel tones. Full body. Storybook style.";
        string anchor = $"{characterName}, {characterDescription}";

        string scene = SummarizeScene(paragraph);

        return $"{ArtStyle} {anchor} is {scene}.";
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
    public static async Task<string> BuildImagePromptWithSceneAsync(
    string characterName,
    string characterDescription,
    string paragraph,
    HttpClient httpClient,
    string apiKey)
    {
        //string style = "Children’s book watercolor illustration. Soft lighting. Gentle pastel tones. Full body. Storybook style.";
        string anchor = $"{characterName}, {characterDescription}";

        // Use GPT to summarize the paragraph visually
        var prompt = $"""
    Summarize the following story paragraph into a short visual scene that can be used in an illustration. Describe only what the main character is doing and what the scene looks like. Do not mention the character’s name or appearance — that will be added separately.

    Paragraph: "{paragraph}"

    Example response: "playing hopscotch with a squirrel under a leafy oak tree"
    """;

        var requestBody = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
            new { role = "user", content = prompt }
        },
            temperature = 0.7,
            max_tokens = 50
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
        return $"""
    Watercolor illustration in the style of Beatrix Potter. Bright, magical colors. Gentle pastel tones. Full-body character centered. Suitable for the front cover of a children’s book. Do not include any text, logos, or physical books. Maintain a consistent illustration style. Featuring {characterName}, {characterDescription}, with a background inspired by: {theme}.
    """;
    }
}
