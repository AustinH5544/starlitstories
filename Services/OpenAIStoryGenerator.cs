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

    public async Task<List<StoryPage>> GenerateStoryAsync(StoryRequest request, bool includeImages = false)
    {
        var prompt = $"""
    Write an 8-paragraph story for young children about {request.CharacterName}, {request.CharacterDescription}, who goes on an adventure in {request.Theme}.
    Use short, clear sentences. Keep language playful and easy to understand. Each paragraph should describe one story scene.
    """;

        var requestBody = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
            new { role = "system", content = "You are a creative children's story writer." },
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

        // Generate image prompts in parallel
        var imagePromptTasks = paragraphs.Select(p =>
            PromptBuilder.BuildImagePromptWithSceneAsync(request.CharacterName, request.CharacterDescription, p, _httpClient, _apiKey)).ToList();

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

        if (includeImages)
        {
            var imageUrls = await GenerateImagesFromPrompts(imagePrompts.ToList());
            for (int i = 0; i < storyPages.Count; i++)
            {
                storyPages[i].ImageUrl = imageUrls[i];
            }
        }

        return storyPages;
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

}
