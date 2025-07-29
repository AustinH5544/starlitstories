using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

public class BlobUploadService
{
    private readonly BlobContainerClient _containerClient;

    public BlobUploadService(IConfiguration config)
    {
        var connectionString = config["AzureBlobStorage:ConnectionString"];
        _containerClient = new BlobContainerClient(connectionString, "story-images"); // container name
        _containerClient.CreateIfNotExists(PublicAccessType.Blob);
    }

    public async Task<string> UploadImageAsync(string imageUrl, string fileName)
    {
        using var httpClient = new HttpClient();
        var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);

        using var stream = new MemoryStream(imageBytes);
        var blobClient = _containerClient.GetBlobClient(fileName);

        await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = "image/png" });

        return blobClient.Uri.ToString();
    }
}