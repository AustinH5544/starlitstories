using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Hackathon_2025.Services;

public class OpenAIImageGeneratorService : IImageGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public OpenAIImageGeneratorService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _apiKey = config["OpenAI:ApiKey"]!;
    }

    public async Task<List<string>> GenerateImagesAsync(List<string> prompts)
    {
        var results = new List<string>();

        foreach (var prompt in prompts)
        {
            var body = new
            {
                model = "dall-e-3",
                prompt,
                n = 1,
                size = "1024x1024"
            };

            using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/generations");
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            msg.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var res = await _httpClient.SendAsync(msg);
            res.EnsureSuccessStatusCode();

            var json = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());
            var url = json.RootElement.GetProperty("data")[0].GetProperty("url").GetString();
            results.Add(url!);
        }

        return results;
    }
}
