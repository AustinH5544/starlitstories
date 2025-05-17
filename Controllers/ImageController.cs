using Hackathon_2025.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImageController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public ImageController(IConfiguration config, HttpClient httpClient)
    {
        _apiKey = config["OpenAI:ApiKey"]!;
        _httpClient = httpClient;
    }

    [HttpPost("generate-batch")]
    public async Task<IActionResult> GenerateImages([FromBody] ImageBatchRequest request)
    {
        var imageUrls = new List<string>();

        foreach (var prompt in request.Prompts)
        {
            var requestBody = new
            {
                model = "dall-e-3",
                prompt = prompt,
                n = 1,
                size = "1024x1024"
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/generations");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, $"Image failed for prompt: {prompt}\n{error}");
            }

            var resultJson = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var url = resultJson.RootElement.GetProperty("data")[0].GetProperty("url").GetString();

            imageUrls.Add(url!);
        }

        return Ok(new { imageUrls });
    }

}
