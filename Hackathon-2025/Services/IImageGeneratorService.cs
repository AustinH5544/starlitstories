namespace Hackathon_2025.Services;

public interface IImageGeneratorService
{
    Task<List<string>> GenerateImagesAsync(List<string> prompts);
}
