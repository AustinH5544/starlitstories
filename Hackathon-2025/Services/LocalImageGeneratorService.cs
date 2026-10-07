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

    public Task<List<string>> GenerateImagesWithCharacterBaseAsync(
        List<string> prompts,
        string characterBasePrompt,
        Action<int, int>? onProgress = null)
        => GenerateImagesAsync(prompts, onProgress); // Local server has no edit/reference support; fall back to sequential.

    public async Task<List<string>> GenerateImagesAsync(
        List<string> prompts,
        Action<int, int>? onProgress = null)
    {
        var results = new List<string>();
        var total = prompts.Count;

        for (int i = 0; i < prompts.Count; i++)
        {
            var body = new
            {
                prompt = prompts[i],
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
            onProgress?.Invoke(i + 1, total);
        }

        return results;
    }
}
