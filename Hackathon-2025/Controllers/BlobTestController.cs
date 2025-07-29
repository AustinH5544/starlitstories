using Microsoft.AspNetCore.Mvc;
using Hackathon_2025.Services;

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BlobTestController : ControllerBase
{
    private readonly BlobUploadService _blobService;

    public BlobTestController(BlobUploadService blobService)
    {
        _blobService = blobService;
    }

    [HttpGet("upload-sample")]
    public async Task<IActionResult> UploadSample()
    {
        // Sample image URL to fetch and upload
        string testImageUrl = "https://i.imgur.com/y2qzG0O.jpeg";

        string fileName = $"test-{Guid.NewGuid()}.png";
        string blobUrl = await _blobService.UploadImageAsync(testImageUrl, fileName);

        return Ok(new { BlobUrl = blobUrl });
    }
}