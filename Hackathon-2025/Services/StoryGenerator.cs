using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hackathon_2025.Models;

namespace Hackathon_2025.Services;

public class StoryGenerator : IStoryGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly IImageGeneratorService _imageService;
    private readonly BlobUploadService _blobUploader;

    public StoryGenerator(
        HttpClient httpClient,
        IConfiguration config,
        IImageGeneratorService imageService,
        BlobUploadService blobUploader)
    {
        _httpClient = httpClient;
        _apiKey = config["OpenAI:ApiKey"]!;
        _imageService = imageService;
        _blobUploader = blobUploader;
    }

    public async Task<StoryResult> GenerateFullStoryAsync(StoryRequest request)
    {
        var characterList = string.Join(", ", request.Characters.Select(c => $"{c.Name} the {c.Role}"));

        // --- NEW: derive friendly style guidance from readingLevel/readerAge
        var style = BuildReadingStyle(request.ReadingLevel, request.ReaderAge);

        var prompt = $"""
Write a complete children's story in 8 paragraphs featuring these characters: {characterList}.
They go on an adventure involving {request.Theme}. The reader already knows what the characters look like,
so do not describe their appearance, clothing, or physical features.

{style.AudienceLine}
Use a {style.Tone} tone. Prefer {style.SentenceRule}
Use {style.VocabHint}.

Structure the story with:
- A clear beginning that introduces the setting and the characters naturally
- A middle that includes a magical or challenging journey
- An ending with a resolution, lesson, or happy conclusion

Each paragraph should represent a different scene in the story.
""";

        var requestBody = new
        {
            // You can keep this model if it's working for you, or switch to a newer one you prefer.
            model = "gpt-3.5-turbo",
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "You are a creative children's story writer. Never describe the characters’ appearance, clothes, hair, or eye color. The story should focus on setting, action, and imagination, not physical traits."
                },
                new { role = "user", content = prompt }
            },
            temperature = 0.8,
            max_tokens = 600
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var storyText = (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("choices")[0]
            .GetProperty("message").GetProperty("content").GetString();

        var paragraphs = storyText!.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Keep your existing async scene prompt builder as-is
        var imagePromptTasks = paragraphs
            .Select(p => PromptBuilder.BuildImagePromptWithSceneAsync(request.Characters, p, _httpClient, _apiKey))
            .ToList();

        var imagePrompts = await Task.WhenAll(imagePromptTasks);

        var storyPages = new List<StoryPage>();
        for (int i = 0; i < paragraphs.Length; i++)
        {
            storyPages.Add(new StoryPage
            {
                Text = paragraphs[i],
                ImagePrompt = imagePrompts[i]
            });
        }

        var externalImageUrls = await _imageService.GenerateImagesAsync(imagePrompts.ToList());

        // Title + Cover (cover prompt now uses readingLevel/readerAge)
        var title = await GenerateTitleAsync(request.Characters.FirstOrDefault()?.Name ?? "A Hero", request.Theme);

        var coverPrompt = PromptBuilder.BuildCoverPrompt(
            request.Characters,
            request.Theme,
            request.ReadingLevel,
            request.ReaderAge
        );

        var coverExternalUrl = (await _imageService.GenerateImagesAsync(new List<string> { coverPrompt }))[0];

        return new StoryResult
        {
            Title = title,
            CoverImagePrompt = coverPrompt,
            CoverImageUrl = coverExternalUrl,
            Pages = storyPages.Select((p, i) =>
            {
                p.ImageUrl = externalImageUrls[i];
                return p;
            }).ToList()
        };
    }

    private async Task<string> GenerateTitleAsync(string characterName, string theme)
    {
        var prompt = $"""
Suggest a creative and whimsical children's book title based on a character named {characterName} and a theme of "{theme}". The title should be short, magical, and memorable.
""";

        var requestBody = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.9,
            max_tokens = 20
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var res = await _httpClient.SendAsync(req);
        res.EnsureSuccessStatusCode();

        var json = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!;
    }

    // --- NEW: small helper that maps reading level + age to text style guidance
    private static ReadingStyle BuildReadingStyle(string? readingLevel, int? readerAge)
    {
        // Defaults
        var tone = "warm and imaginative";
        var sentenceRule = "short sentences (about 10 words on average)";
        var vocabHint = "simple, common words and clear phrasing";
        var audienceLine = readerAge.HasValue
            ? $"Aim for a {readerAge}-year-old reader."
            : "Aim for the selected reading level.";

        switch ((readingLevel ?? "").Trim().ToLowerInvariant())
        {
            case "pre":
                tone = "soothing, playful, and rhythmic";
                sentenceRule = "very short sentences (around 6–8 words)";
                vocabHint = "very common sight words and familiar phrases";
                if (readerAge.HasValue && readerAge <= 4)
                {
                    sentenceRule = "very short sentences (about 5–7 words)";
                    vocabHint = "repetitive patterns and very common words";
                }
                break;

            case "early":
                tone = "friendly and engaging";
                sentenceRule = "short sentences (about 8–12 words)";
                vocabHint = "age-appropriate vocabulary with simple connectors";
                if (readerAge.HasValue && readerAge <= 6)
                {
                    sentenceRule = "short sentences (about 8–10 words)";
                }
                break;

            case "independent":
                tone = "adventurous and lively";
                sentenceRule = "mostly short sentences with occasional longer ones (10–14 words)";
                vocabHint = "age-appropriate vocabulary with a few vivid verbs";
                if (readerAge.HasValue && readerAge <= 8)
                {
                    sentenceRule = "short, lively sentences (about 10–12 words)";
                }
                break;
        }

        return new ReadingStyle(tone, sentenceRule, vocabHint, audienceLine);
    }

    private readonly record struct ReadingStyle(
        string Tone,
        string SentenceRule,
        string VocabHint,
        string AudienceLine
    );
}
