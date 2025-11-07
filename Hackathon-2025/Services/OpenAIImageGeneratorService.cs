using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Hackathon_2025.Services;

public class OpenAIImageGeneratorService : IImageGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<OpenAIImageGeneratorService> _logger;

    public OpenAIImageGeneratorService(HttpClient httpClient, IConfiguration config, ILogger<OpenAIImageGeneratorService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["OpenAI:ApiKey"]!;
        _logger = logger;
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

            // ⬇️ ⬇️ INSERTED: log error body on non-2xx, then throw
            var raw = await res.Content.ReadAsStringAsync(); // read first so it’s available even on error
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "OpenAI images error {Status}. PromptLen={Len}. Body={Body}",
                    (int)res.StatusCode,
                    prompt?.Length ?? 0,
                    raw
                );
                throw new HttpRequestException($"Images API failed ({(int)res.StatusCode}). Body: {raw}");
            }
            // ⬆️ ⬆️ END INSERT

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
            using var json = await JsonDocument.ParseAsync(stream);
            var url = json.RootElement.GetProperty("data")[0].GetProperty("url").GetString();
            results.Add(url!);
        }

        return results;
    }
}