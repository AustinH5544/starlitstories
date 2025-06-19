using Hackathon_2025.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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

    public ImageController(IOptions<OpenAISettings> options, HttpClient httpClient)
    {
        _apiKey = options.Value.ApiKey;
        _httpClient = httpClient;
    }

    [HttpPost("generate-batch")]
    public async Task<IActionResult> GenerateImages([FromBody] ImageBatchRequest request)
    {
        if (request.Prompts == null || request.Prompts.Count == 0)
        {
            return BadRequest("Prompts list cannot be empty.");
        }

        var imageResults = new List<string>();

        foreach (var prompt in request.Prompts)
        {
            var requestBody = new
            {
                prompt = prompt,
                guidance_scale = 7.5,
                num_inference_steps = 25,
                seed = 1337
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "http://192.168.0.11:5000/generate"); // Replace with actual imageFX endpoint
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, $"Image generation failed for prompt: {prompt}\n{error}");
            }

            var resultJson = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var imageBase64 = resultJson.RootElement.GetProperty("image_base64").GetString();

            imageResults.Add(imageBase64!); // You can optionally prepend "data:image/png;base64," if sending directly to frontend
        }

        return Ok(new { images = imageResults });
    }
}
