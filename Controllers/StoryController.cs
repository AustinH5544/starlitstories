using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hackathon_2025.Models;

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoryController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public StoryController(HttpClient httpClient, IOptions<OpenAISettings> openAiSettings)
    {
        _httpClient = httpClient;
        _apiKey = openAiSettings.Value.ApiKey;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateStory([FromBody] StoryRequest request)
    {
        var prompt = $"Write a 5-paragraph children's story about {request.CharacterName}, {request.CharacterDescription}, who goes on an adventure in {request.Theme}. Make the story imaginative, fun, and appropriate for young readers.";

        var requestBody = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
                new { role = "system", content = "You are a helpful and creative children's story writer." },
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

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, error);
        }

        using var responseStream = await response.Content.ReadAsStreamAsync();
        var result = await JsonDocument.ParseAsync(responseStream);
        var storyText = result.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        // Split story into paragraphs for frontend pagination
        var pages = storyText.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        return Ok(new { pages });
    }
}
