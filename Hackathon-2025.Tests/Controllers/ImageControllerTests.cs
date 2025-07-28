using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Controllers;
using Hackathon_2025.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class ImageControllerTests
{
    private ImageController _controller = null!;
    private Mock<IOptions<OpenAISettings>> _mockOptions = null!;
    private Mock<HttpMessageHandler> _mockHandler = null!;
    private HttpClient _httpClient = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange OpenAISettings
        _mockOptions = new Mock<IOptions<OpenAISettings>>();
        _mockOptions.Setup(opt => opt.Value).Returns(new OpenAISettings { ApiKey = "fake-key" });

        // Create HttpClient with mocked HttpMessageHandler
        _mockHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost") // Required but unused
        };

        _controller = new ImageController(_mockOptions.Object, _httpClient);
    }

    [TestMethod]
    public async Task GenerateImages_ValidPrompts_ReturnsOkWithImages()
    {
        // Arrange
        var base64Image = Convert.ToBase64String(Encoding.UTF8.GetBytes("fake-image"));
        var fakeJson = $"{{ \"image_base64\": \"{base64Image}\" }}";

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(fakeJson, Encoding.UTF8, "application/json")
            });

        var request = new ImageBatchRequest
        {
            Prompts = new List<string> { "dragon in the sky" }
        };

        // Act
        var result = await _controller.GenerateImages(request) as OkObjectResult;

        // Assert
        Assert.IsNotNull(result);

        // Convert to JSON then back to typed object
        var json = JsonSerializer.Serialize(result.Value);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);

        Assert.IsNotNull(parsed);
        Assert.IsTrue(parsed!.ContainsKey("images"));
        Assert.AreEqual(1, parsed["images"].Count);
        Assert.AreEqual(base64Image, parsed["images"][0]);
    }

    [TestMethod]
    public async Task GenerateImages_EmptyPromptList_ReturnsBadRequest()
    {
        // Arrange
        var request = new ImageBatchRequest { Prompts = new List<string>() };

        // Act
        var result = await _controller.GenerateImages(request) as BadRequestObjectResult;

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Prompts list cannot be empty.", result.Value);
    }

    [TestMethod]
    public async Task GenerateImages_ImageServiceFails_ReturnsServerError()
    {
        // Arrange
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Service error")
            });

        var request = new ImageBatchRequest
        {
            Prompts = new List<string> { "fail prompt" }
        };

        // Act
        var result = await _controller.GenerateImages(request) as ObjectResult;

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(500, result.StatusCode);
        Assert.IsTrue(result.Value?.ToString()?.Contains("Image generation failed") == true);
    }
}
