using System.Text;
using System.Text.Json;

namespace Hackathon_2025.Services;

public class LocalImageGeneratorService : IImageGeneratorService
{
    private readonly HttpClient _httpClient;

    public LocalImageGeneratorService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<string>> GenerateImagesAsync(List<string> prompts)
    {
        var results = new List<string>();

        foreach (var prompt in prompts)
        {
            var body = new
            {
                prompt,
                guidance_scale = 7.5,
                num_inference_steps = 25,
                seed = 1337
            };

            using var msg = new HttpRequestMessage(HttpMethod.Post, "http://192.168.0.11:5000/generate");
            msg.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var res = await _httpClient.SendAsync(msg);
            res.EnsureSuccessStatusCode();

            var json = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());
            var base64 = json.RootElement.GetProperty("image_base64").GetString();
            results.Add("data:image/png;base64," + base64);
        }

        return results;
    }
}
