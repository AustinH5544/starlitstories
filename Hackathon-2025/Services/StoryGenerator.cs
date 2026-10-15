using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using Hackathon_2025.Models;

namespace Hackathon_2025.Services;

public class StoryGenerator : IStoryGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly IImageGeneratorService _imageService;
    private readonly IBlobUploadService _blobUploader; // kept for DI consistency (unused here)
    private readonly ILogger<StoryGenerator> _logger;
    private readonly IConfiguration _config;

    public StoryGenerator(
        HttpClient httpClient,
        IConfiguration config,
        IImageGeneratorService imageService,
        IBlobUploadService blobUploader,
        ILogger<StoryGenerator> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["OpenAI:ApiKey"]!;
        _imageService = imageService;
        _blobUploader = blobUploader;
        _logger = logger;
        _config = config;
    }

    public async Task<StoryResult> GenerateFullStoryAsync(
        StoryRequest request,
        Action<ProgressUpdate>? onProgress = null)
    {
        // Normalize characters list so it's never null
        var characters = request.Characters ?? new List<CharacterSpec>();

        // SINGLE SOURCE OF TRUTH: page count from config only
        int pageCount = _config.GetValue<int?>("Story:DefaultParagraphs") ?? 10;
        var mustLesson = !string.IsNullOrWhiteSpace(request.LessonLearned);

        var systemContent = StoryPrompt.BuildSystemPrompt(mustLesson);
        var prompt = StoryPrompt.BuildUserPrompt(request, characters, pageCount);

        _logger?.LogInformation("=== STORY PROMPT ===\n{Prompt}\n====================", prompt);

        // Token budget for exact count
        var maxTokens = Math.Min(1800, pageCount * 110 + 300);

        var requestBody = new
        {
            model = "gpt-4.1-mini",
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

        onProgress?.Invoke(new ProgressUpdate
        {
            Stage = "story",
            Percent = 15,
            Message = "Writing your story..."
        });

        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var storyText = (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("choices")[0]
            .GetProperty("message").GetProperty("content").GetString() ?? "";

        // ---- Sanitize output ----
        storyText = Regex.Replace(storyText, @"(?m)^\s*```.*$", "");                    // code fences
        storyText = Regex.Replace(storyText, @"(?mi)^\s*Page\s*\d+\s*$", "");           // "Page N" lines
        storyText = Regex.Replace(storyText, @"[ \t]+$", "", RegexOptions.Multiline);   // trailing spaces
        storyText = Regex.Replace(storyText, @"\n{3,}", "\n\n").Trim();                 // collapse blanks

        // Normalize line endings
        storyText = storyText.Replace("\r\n", "\n").Replace("\r", "\n");

        // ---- Extract (and strip) Lesson line, if present AND required ----
        string? extractedLesson = null;
        if (mustLesson)
        {
            var lessonMatch = Regex.Match(storyText, @"(?mi)^\s*Lesson:\s*(.+)\s*$");
            if (lessonMatch.Success)
            {
                extractedLesson = lessonMatch.Groups[1].Value.Trim();
                storyText = Regex.Replace(storyText, @"(?mi)^\s*Lesson:.*$", "").Trim();
            }

            // If we required a moral but didn't get one explicitly, fall back to the requested lesson text
            if (string.IsNullOrWhiteSpace(extractedLesson))
                extractedLesson = request.LessonLearned?.Trim();
        }

        // --- Split into pages ---
        static string[] SplitIntoPages(string? text)
        {
            var t = (text ?? string.Empty).Trim();
            if (t.Length == 0) return Array.Empty<string>();

            // 1) Preferred: explicit delimiter lines containing only ---
            var explicitParts = Regex
                .Split(t, @"^\s*---\s*$", RegexOptions.Multiline)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();
            if (explicitParts.Length > 1)
                return explicitParts;

            // 1b) Inline delimiters: " ... --- ... "
            var inlineParts = Regex
                .Split(t, @"\s+---\s+")
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();
            if (inlineParts.Length > 1)
                return inlineParts;

            // 2) Fallbacks: blank lines OR single newline after sentence end before a capitalized start
            var fallbackParts = Regex
                .Split(t, @"\r?\n\s*\r?\n+|(?<=\S[.!?])\s*\r?\n(?=\p{Lu})")
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();

            return fallbackParts;
        }

        var parts = SplitIntoPages(storyText).ToList();

        // Guarantee exactly N non-empty paragraphs (server-only control)
        parts = EnsureExactlyN(parts, pageCount);

        var paragraphs = parts.ToArray();

        _logger?.LogInformation("Story paragraphs: got={Got}, target={Target}", paragraphs.Length, pageCount);

        // Use a copy WITHOUT the Lesson line for image prompts
        var paragraphsForImages = paragraphs.ToArray();

        // Append the Lesson as an extra line to the last page text only (no image comes from it)
        if (mustLesson && !string.IsNullOrWhiteSpace(extractedLesson) && paragraphs.Length > 0)
            paragraphs[^1] = paragraphs[^1].TrimEnd() + "\n\nLesson: " + extractedLesson;

        // Image prompts strictly from scene paragraphs (no "Lesson:" line)
        onProgress?.Invoke(new ProgressUpdate
        {
            Stage = "scene-prompts",
            Percent = 28,
            Message = "Planning each illustration..."
        });

        // Each scene sees the whole story so the setting stays consistent across pages.
        var imagePromptTasks = paragraphsForImages.Select((p, i) =>
            PromptBuilder.BuildImagePromptAsync(characters, p, _httpClient, _apiKey, request.ArtStyle,
                request.Theme, paragraphsForImages, i));
        var imagePrompts = await Task.WhenAll(imagePromptTasks);

        _logger?.LogInformation("Image prompts generated: count={Count}", imagePrompts.Length);

        // Build cover prompt — story-aware: LLM picks the most iconic scene from the full story.
        onProgress?.Invoke(new ProgressUpdate
        {
            Stage = "cover-prompt",
            Percent = 38,
            Message = "Choosing the best cover moment..."
        });

        var coverPrompt = await PromptBuilder.BuildCoverPromptAsync(
            characters, request.Theme, request.ReadingLevel, request.ArtStyle,
            paragraphsForImages, _httpClient, _apiKey);

        // Build the base character prompt used as the shared reference image.
        var charBasePrompt = PromptBuilder.BuildBaseCharacterPrompt(characters, request.ArtStyle);

        // Generate: base character first (1 gpt-image-2 call), then all story images
        // (cover + pages) in parallel as gpt-image-2 edits referencing the base.
        onProgress?.Invoke(new ProgressUpdate
        {
            Stage = "character-base",
            Percent = 48,
            Message = "Painting your character guide..."
        });

        var allPrompts = new[] { coverPrompt }.Concat(imagePrompts).ToList();
        onProgress?.Invoke(new ProgressUpdate
        {
            Stage = "page-images",
            Percent = 58,
            Message = $"Illustrating your book (0/{allPrompts.Count})...",
            Index = 0,
            Total = allPrompts.Count
        });

        var allImageUrls = await _imageService.GenerateImagesWithCharacterBaseAsync(
            allPrompts,
            charBasePrompt,
            (completed, total) =>
            {
                var percent = 58 + (int)Math.Round((completed / (double)Math.Max(1, total)) * 24);
                onProgress?.Invoke(new ProgressUpdate
                {
                    Stage = "page-images",
                    Percent = Math.Min(82, percent),
                    Message = $"Illustrating your book ({completed}/{total})...",
                    Index = completed,
                    Total = total
                });
            });

        var coverExternalUrl = allImageUrls[0];
        var externalImageUrls = allImageUrls.Skip(1).ToList();

        // Title is independent — generate it in parallel would be ideal but keeping it simple here.
        onProgress?.Invoke(new ProgressUpdate
        {
            Stage = "title",
            Percent = 84,
            Message = "Naming your story..."
        });

        var title = await GenerateTitleAsync(characters.FirstOrDefault()?.Name ?? "A Hero", request.Theme);

        // Assemble DTO pages (NOT EF entities) with image prompts included
        var dtoPages = paragraphs
            .Select((text, i) => new StoryPageDto(text, imagePrompts[i], externalImageUrls.ElementAtOrDefault(i)))
            .ToList();

        return new StoryResult
        {
            Title = title,
            CoverImagePrompt = coverPrompt,
            CoverImageUrl = coverExternalUrl,
            Pages = dtoPages
        };
    }

    // Split sentences on end punctuation followed by a capital letter (simple heuristic).
    private static readonly Regex SentenceSplit =
        new(@"(?<=[.!?])\s+(?=\p{Lu})", RegexOptions.Multiline);

    // Ensure exactly N non-empty paragraphs by splitting long ones or merging short ones.
    private static List<string> EnsureExactlyN(List<string> parts, int target)
    {
        // 0) Normalize: trim and drop empties
        parts = parts.Select(p => (p ?? string.Empty).Trim())
                     .Where(p => p.Length > 0)
                     .ToList();

        // Edge case: nothing at all -> seed a stub
        if (parts.Count == 0)
            parts.Add("The adventure begins with a vivid moment in the story.");

        // 1) If too few, split the longest paragraphs by sentences until we reach target
        while (parts.Count < target)
        {
            int idx = Enumerable.Range(0, parts.Count)
                                .OrderByDescending(i => parts[i].Length)
                                .First();

            var segs = SentenceSplit.Split(parts[idx])
                                    .Where(s => !string.IsNullOrWhiteSpace(s))
                                    .ToArray();

            if (segs.Length >= 2)
            {
                int half = (int)Math.Ceiling(segs.Length / 2.0);
                var left = string.Join(" ", segs.Take(half)).Trim();
                var right = string.Join(" ", segs.Skip(half)).Trim();

                parts[idx] = left.Length > 0 ? left : parts[idx];
                if (right.Length > 0) parts.Insert(idx + 1, right);
            }
            else
            {
                // fallback: split by midpoint if we can't find sentence boundaries
                var text = parts[idx];
                if (text.Length < 80)
                {
                    // very short; insert a safe continuation to avoid blanks
                    parts.Insert(idx + 1, "The adventure continues with a vivid moment in the story.");
                }
                else
                {
                    int mid = text.Length / 2;
                    var left = text.Substring(0, mid).Trim();
                    var right = text.Substring(mid).Trim();
                    parts[idx] = left.Length > 0 ? left : text;
                    if (right.Length > 0) parts.Insert(idx + 1, right);
                }
            }
        }

        // 2) If too many, merge the shortest neighbor pairs until we reach target
        while (parts.Count > target)
        {
            int idx = Enumerable.Range(0, parts.Count).OrderBy(i => parts[i].Length).First();

            int neighbor = (idx == parts.Count - 1) ? idx - 1
                        : (idx == 0) ? 1
                        : (parts[idx - 1].Length <= parts[idx + 1].Length ? idx - 1 : idx + 1);

            int a = Math.Min(idx, neighbor);
            int b = Math.Max(idx, neighbor);

            parts[a] = (parts[a] + " " + parts[b]).Trim();
            parts.RemoveAt(b);
        }

        // 3) Final safety: replace any accidental empties
        for (int i = 0; i < parts.Count; i++)
            if (string.IsNullOrWhiteSpace(parts[i]))
                parts[i] = "The adventure continues with a vivid moment in the story.";

        return parts;
    }

    private async Task<string> GenerateTitleAsync(string characterName, string theme)
    {
        var prompt = $"""
Suggest a creative and whimsical children's book title based on a character named {characterName}
and a theme of "{theme}". Return ONLY the title text — no quotes, no punctuation at the ends,
no extra words, no Markdown.
""";

        var requestBody = new
        {
            model = "gpt-4.1-mini",
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.9,
            max_tokens = 20
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var res = await _httpClient.SendAsync(req);
        res.EnsureSuccessStatusCode();

        var json = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());
        var raw = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

        // strip code fences, newlines, and surrounding quotes/backticks/smart quotes
        var cleaned = Regex.Replace(raw, @"(?m)^\s*```.*$|^\s*>.*$", "");
        cleaned = cleaned.Replace("\r", "").Replace("\n", " ").Trim();

        // remove surrounding quotes/backticks (straight or smart) and trailing period
        cleaned = Regex.Replace(cleaned, @"^[\s""'“”‘’`]+", "");
        cleaned = Regex.Replace(cleaned, @"[\s""'“”‘’`]+$", "");
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();

        return cleaned;
    }
}
