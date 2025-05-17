using Hackathon_2025.Models;

namespace Hackathon_2025.Services;

public interface IStoryGeneratorService
{
    Task<List<string>> GenerateStoryAsync(StoryRequest request);
}
