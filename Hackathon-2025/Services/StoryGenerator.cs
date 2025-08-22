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
    private readonly ILogger<StoryGenerator> _logger;

    public StoryGenerator(
    HttpClient httpClient,
    IConfiguration config,
    IImageGeneratorService imageService,
    BlobUploadService blobUploader,
    ILogger<StoryGenerator> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["OpenAI:ApiKey"]!;
        _imageService = imageService;
        _blobUploader = blobUploader;
        _logger = logger;
    }

    public async Task<StoryResult> GenerateFullStoryAsync(StoryRequest request)
    {
        var characterList = string.Join(", ", request.Characters.Select(c =>
            c.Role?.ToLowerInvariant() == "main" ? c.Name : $"{c.Name} the {c.Role}"
        ));

        var style = BuildReadingStyle(request.ReadingLevel);
        var pageCount = request.PageCount ?? 8; // length hint from controller ("short"=4, "medium"=8, "long"=12)
        var mustLesson = !string.IsNullOrWhiteSpace(request.LessonLearned);

        var systemContent =
            "You are a creative children's story writer. " +
            "Never describe physical appearance. " +
            "If a lesson is provided, weave it naturally into the plot and always conclude with a final line that begins with 'Lesson:'.";

        // Ask for an extra Lesson line AFTER the story paragraphs (not a paragraph itself)
        var lessonLine = mustLesson
            ? $"""
Ensure the story naturally reflects this moral: "{request.LessonLearned}".
After you finish the {pageCount} paragraphs, write one extra line that begins with "Lesson:" and states this moral in simple words. Do not count this line as a paragraph.
"""
            : "If a gentle lesson emerges naturally, keep it subtle.";

        // Prevent headings or Page labels
        var formatRule = "Write plain paragraphs only. Do NOT add any headings, numbers, or 'Page' labels.";

        var prompt = $"""
Write a complete children's story of about {pageCount} short paragraphs featuring these characters: {characterList}.
They go on an adventure involving {request.Theme}. The reader already knows what the characters look like,
so do not describe their appearance, clothing, or physical features.

{style.AudienceLine}
Use a {style.Tone} tone. Prefer {style.SentenceRule}
Use {style.VocabHint}.
{formatRule}
{lessonLine}

Structure the story with:
- A clear beginning that introduces the setting and the characters naturally
- A middle that includes a magical or challenging journey
- Keep paragraphs separated by a blank line

Each paragraph should represent a different scene in the story.
""";

        _logger?.LogInformation("=== STORY PROMPT ===\n{Prompt}\n====================", prompt);

        // Token budget proportional to target length
        var maxTokens = Math.Min(1500, pageCount * 90 + 200);

        var requestBody = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
            new { role = "system", content = systemContent },
            new { role = "user",   content = prompt }
        },
            temperature = 0.6,
            max_tokens = maxTokens
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var storyText = (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("choices")[0]
            .GetProperty("message").GetProperty("content").GetString() ?? "";

        // ---- Sanitize output & extract lesson ----
        // Remove any stray 'Page N' lines, trailing spaces, collapse extra blanks
        storyText = System.Text.RegularExpressions.Regex.Replace(storyText, @"(?mi)^\s*Page\s*\d+\s*$", "");
        storyText = System.Text.RegularExpressions.Regex.Replace(storyText, @"[ \t]+$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
        storyText = System.Text.RegularExpressions.Regex.Replace(storyText, @"\n{3,}", "\n\n").Trim();

        string? extractedLesson = null;
        if (mustLesson)
        {
            var m = System.Text.RegularExpressions.Regex.Match(storyText, @"(?mi)^\s*Lesson:\s*(.+)\s*$");
            if (m.Success)
            {
                extractedLesson = m.Groups[1].Value.Trim();
                storyText = System.Text.RegularExpressions.Regex.Replace(storyText, @"(?mi)^\s*Lesson:.*$", "").Trim();
            }
        }

        // Scenes only: split into real, non-empty paragraphs (NO padding)
        var paragraphs = storyText
            .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToArray();

        _logger?.LogInformation("Story paragraphs: got={Got}, targetHint={Target}", paragraphs.Length, pageCount);

        // Use a copy WITHOUT the Lesson line for image prompts
        var paragraphsForImages = paragraphs.ToArray();

        // Safety net: if lesson missing, fall back to requested moral
        if (mustLesson && string.IsNullOrWhiteSpace(extractedLesson))
            extractedLesson = request.LessonLearned?.Trim();

        // Append the Lesson as an extra line to the last page text only (no image comes from it)
        if (mustLesson && !string.IsNullOrWhiteSpace(extractedLesson) && paragraphs.Length > 0)
            paragraphs[^1] = paragraphs[^1].TrimEnd() + "\n\nLesson: " + extractedLesson;

        // Image prompts strictly from scene paragraphs
        var imagePromptTasks = paragraphsForImages.Select(p =>
            PromptBuilder.BuildImagePromptAsync(request.Characters, p, _httpClient, _apiKey, request.ArtStyle));
        var imagePrompts = await Task.WhenAll(imagePromptTasks);

        _logger?.LogInformation("Image prompts generated: count={Count}", imagePrompts.Length);

        // Assemble pages (images match scene paragraphs count)
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

        var title = await GenerateTitleAsync(request.Characters.FirstOrDefault()?.Name ?? "A Hero", request.Theme);

        var coverPrompt = PromptBuilder.BuildCoverPrompt(
            request.Characters, request.Theme, request.ReadingLevel, request.ArtStyle);

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

    // --- Simplified: map reading level to text style guidance (no readerAge)
    private static ReadingStyle BuildReadingStyle(string? readingLevel)
    {
        // Defaults
        var tone = "warm and imaginative";
        var sentenceRule = "short sentences (about 10 words on average)";
        var vocabHint = "simple, common words and clear phrasing";
        var audienceLine = "Aim for the selected reading level.";

        switch ((readingLevel ?? "").Trim().ToLowerInvariant())
        {
            case "pre":
                tone = "soothing, playful, and rhythmic";
                sentenceRule = "very short sentences (around 6–8 words)";
                vocabHint = "very common sight words and familiar phrases";
                break;

            case "early":
                tone = "friendly and engaging";
                sentenceRule = "short sentences (about 8–12 words)";
                vocabHint = "age-appropriate vocabulary with simple connectors";
                break;

            case "independent":
                tone = "adventurous and lively";
                sentenceRule = "mostly short sentences with occasional longer ones (10–14 words)";
                vocabHint = "age-appropriate vocabulary with a few vivid verbs";
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
