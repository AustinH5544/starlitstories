namespace Hackathon_2025.Options;

public class AzureBlobStorageOptions
{
    public string ConnectionString { get; init; } = "";
    public string ContainerName { get; init; } = "story-images";
}