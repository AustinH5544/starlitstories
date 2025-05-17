using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hackathon_2025.Models;
using Hackathon_2025.Services;

public class OpenAIStoryGenerator : IStoryGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public OpenAIStoryGenerator(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _apiKey = config["OpenAI:ApiKey"]!;
    }

    public async Task<List<string>> GenerateStoryAsync(StoryRequest request)
    {
        var prompt = $"Write an 8-paragraph story for young children about {request.CharacterName}, {request.CharacterDescription}, who goes on an adventure in {request.Theme}. Use short, clear sentences. Keep language playful and easy to understand. Each paragraph should describe one story scene.";

        var requestBody = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
                new { role = "system", content = "You are a creative children's story writer." },
                new { role = "user", content = prompt }
            },
            temperature = 0.8,
            max_tokens = 600
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        using var responseStream = await response.Content.ReadAsStreamAsync();
        var result = await JsonDocument.ParseAsync(responseStream);
        var storyText = result.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return storyText!.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}
