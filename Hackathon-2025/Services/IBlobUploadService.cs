namespace Hackathon_2025.Services;

public interface IBlobUploadService
{
    Task<string> UploadImageAsync(string imageUrl, string fileName);
    Task<string> UploadBase64ImageAsync(string base64Data, string fileName);
    Task DeleteByUrlAsync(string blobUrl);
}
