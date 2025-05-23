using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hackathon_2025.Models;

namespace Hackathon_2025.Services;

public class OpenAIStoryGenerator : IStoryGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public OpenAIStoryGenerator(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _apiKey = config["OpenAI:ApiKey"]!;
    }

    public async Task<StoryResult> GenerateFullStoryAsync(StoryRequest request)
    {
        var characterList = string.Join(", ", request.Characters.Select(c => $"{c.Name} the {c.Role}"));

        var prompt = $"""
Write a complete children's story in 8 paragraphs featuring these characters: {characterList}.
They go on an adventure involving {request.Theme}. The reader already knows what the characters look like,
so do not describe their appearance, clothing, or physical features.

Structure the story with:
- A clear beginning that introduces the setting and the characters naturally
- A middle that includes a magical or challenging journey
- An ending with a resolution, lesson, or happy conclusion

Use simple, imaginative language and short, playful sentences. Each paragraph should represent a different scene in the story.
""";

        var requestBody = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "You are a creative children's story writer. Never describe the characters’ appearance, clothes, hair, or eye color. The story should focus on setting, action, and imagination, not physical traits."
                },
                new { role = "user", content = prompt }
            },
            temperature = 0.8,
            max_tokens = 800
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var storyText = (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("choices")[0]
            .GetProperty("message").GetProperty("content").GetString();

        var paragraphs = storyText!.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        var imagePromptTasks = paragraphs.Select(p =>
            PromptBuilder.BuildImagePromptWithSceneAsync(request.Characters, p, _httpClient, _apiKey)).ToList();

        var imagePrompts = await Task.WhenAll(imagePromptTasks);

        var storyPages = new List<StoryPage>();

        for (int i = 0; i < paragraphs.Count(); i++)
        {
            storyPages.Add(new StoryPage
            {
                Text = paragraphs[i],
                ImagePrompt = imagePrompts[i]
            });
        }

        var imageUrls = await GenerateImagesFromPrompts(imagePrompts.ToList());

        for (int i = 0; i < storyPages.Count; i++)
        {
            storyPages[i].ImageUrl = imageUrls[i];
        }

        var title = await GenerateTitleAsync(request.Characters.FirstOrDefault()?.Name ?? "A Hero", request.Theme);
        var coverPrompt = PromptBuilder.BuildCoverPrompt(request.Characters, request.Theme);
        var coverUrl = (await GenerateImagesFromPrompts(new List<string> { coverPrompt }))[0];

        return new StoryResult
        {
            Title = title,
            CoverImagePrompt = coverPrompt,
            CoverImageUrl = coverUrl,
            Pages = storyPages
        };
    }

    private async Task<List<string>> GenerateImagesFromPrompts(List<string> prompts)
    {
        var imageUrls = new List<string>();

        foreach (var prompt in prompts)
        {
            var req = new
            {
                model = "dall-e-3",
                prompt = prompt,
                n = 1,
                size = "1024x1024"
            };

            using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/generations");
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            msg.Content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json");

            var res = await _httpClient.SendAsync(msg);
            res.EnsureSuccessStatusCode();

            var resultJson = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());
            var url = resultJson.RootElement.GetProperty("data")[0].GetProperty("url").GetString();
            imageUrls.Add(url!);
        }

        return imageUrls;
    }

    private async Task<string> GenerateTitleAsync(string characterName, string theme)
    {
        var prompt = $"""
Suggest a creative and whimsical children's book title based on a character named {characterName} and a theme of "{theme}". The title should be short, magical, and memorable.
""";

        var requestBody = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.9,
            max_tokens = 20
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var res = await _httpClient.SendAsync(req);
        res.EnsureSuccessStatusCode();

        var json = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!;
    }
}
