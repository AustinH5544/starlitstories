namespace Hackathon_2025.Services;

public interface IImageGeneratorService
{
    Task<List<string>> GenerateImagesAsync(List<string> prompts);

    // Generate a base character image first, then all prompts in parallel as edits referencing it.
    Task<List<string>> GenerateImagesWithCharacterBaseAsync(
        List<string> prompts,
        string characterBasePrompt);
}
