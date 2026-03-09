using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Hackathon_2025.Services;

public class OpenAIImageGeneratorService : IImageGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<OpenAIImageGeneratorService> _logger;

    public OpenAIImageGeneratorService(HttpClient httpClient, IConfiguration config, ILogger<OpenAIImageGeneratorService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["OpenAI:ApiKey"]!;
        _logger = logger;
    }

    public async Task<List<string>> GenerateImagesAsync(List<string> prompts)
    {
        var results = new List<string>();
        string? referenceImageUrl = null;

        for (int i = 0; i < prompts.Count; i++)
        {
            if (i == 0 || referenceImageUrl == null)
            {
                // First image (or single-image calls like cover): use DALL-E 3.
                var url = await GenerateSingleImageAsync(prompts[i]);
                results.Add(url);

                // Only save as reference when generating a multi-page set.
                if (i == 0 && prompts.Count > 1)
                    referenceImageUrl = url;
            }
            else
            {
                // Pages 2+: use gpt-image-1 edits with page 1 as the character reference.
                var dataUri = await GenerateSingleImageWithReferenceAsync(prompts[i], referenceImageUrl, null);
                results.Add(dataUri);
            }
        }

        return results;
    }

    // --- DALL-E 3 path (page 1 + cover) ---------------------------------------

    private async Task<string> GenerateSingleImageAsync(string originalPrompt)
    {
        string workingPrompt = originalPrompt ?? string.Empty;
        bool trimmedForRetry = false;

        const int maxAttempts = 4;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var msg = BuildDallE3Request(workingPrompt);
            _logger.LogInformation("PROMPT[image->dall-e-3] attempt={Attempt} len={Len}\n{Prompt}\n",
                attempt, workingPrompt.Length, workingPrompt);

            string? raw = null;
            string? reqId = null;
            int status = 0;

            try
            {
                using var res = await _httpClient.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead);
                res.Headers.TryGetValues("x-request-id", out var reqIds);
                reqId = reqIds?.FirstOrDefault();
                status = (int)res.StatusCode;
                raw = await res.Content.ReadAsStringAsync();

                if (res.IsSuccessStatusCode)
                {
                    _logger.LogInformation("RESULT[image<-dall-e-3] ReqId={ReqId} bytes={Bytes}",
                        reqId, raw.Length);

                    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
                    using var json = await JsonDocument.ParseAsync(stream);
                    return json.RootElement.GetProperty("data")[0].GetProperty("url").GetString()!;
                }

                _logger.LogError("OpenAI images error {Status}. Attempt={Attempt} ReqId={ReqId} PromptLen={Len}. Body={Body}",
                    status, attempt, reqId, workingPrompt.Length, raw);

                if (attempt < maxAttempts && IsTransient(status))
                {
                    if (!trimmedForRetry && LooksLarge(workingPrompt))
                    {
                        var trimmed = TrimToMaxWords(workingPrompt, 2000);
                        if (!string.Equals(trimmed, workingPrompt, StringComparison.Ordinal))
                        {
                            _logger.LogWarning("Retrying with trimmed prompt. OldLen={OldLen} NewLen={NewLen} ReqId={ReqId}",
                                workingPrompt.Length, trimmed.Length, reqId);
                            workingPrompt = trimmed;
                            trimmedForRetry = true;
                        }
                    }

                    await Task.Delay(BackoffDelayMs(attempt));
                    continue;
                }

                throw new HttpRequestException($"Images API failed ({status}). ReqId={reqId}. Body: {raw}");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Images request timed out. Attempt={Attempt}", attempt);

                if (attempt < maxAttempts)
                {
                    if (!trimmedForRetry && LooksLarge(workingPrompt))
                    {
                        var trimmed = TrimToMaxWords(workingPrompt, 2000);
                        if (!string.Equals(trimmed, workingPrompt, StringComparison.Ordinal))
                        {
                            _logger.LogWarning("Retrying (timeout) with trimmed prompt. OldLen={OldLen} NewLen={NewLen}",
                                workingPrompt.Length, trimmed.Length);
                            workingPrompt = trimmed;
                            trimmedForRetry = true;
                        }
                    }

                    await Task.Delay(BackoffDelayMs(attempt));
                    continue;
                }

                throw;
            }
        }

        throw new InvalidOperationException("GenerateSingleImageAsync: exhausted all attempts.");
    }

    private HttpRequestMessage BuildDallE3Request(string prompt)
    {
        var body = new
        {
            model = "dall-e-3",
            prompt,
            n = 1,
            size = "1024x1024",
            style = "vivid"
        };

        var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/generations");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        msg.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return msg;
    }

    // --- gpt-image-1 edits path (pages 2+) ------------------------------------

    // Dedicated client for the edits endpoint — avoids TLS connection-reuse issues
    // (Windows Schannel SEC_E_MESSAGE_ALTERED) when mixing JSON and multipart on the same pool.
    // PooledConnectionLifetime prevents stale-connection resets: OpenAI closes idle connections
    // after ~90s; we rotate ours every 60s so we never try to reuse a dead socket.
    private static readonly HttpClient _editsClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromSeconds(60),
        PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
    });

    public async Task<List<string>> GenerateImagesWithCharacterBaseAsync(
        List<string> prompts, string characterBasePrompt)
    {
        // Step 1: Generate the base character image using DALL-E 3.
        string characterUrl = await GenerateSingleImageAsync(characterBasePrompt);

        // Step 2: Download the character bytes once; all parallel edits share this.
        byte[] characterBytes = await _editsClient.GetByteArrayAsync(characterUrl);

        // Step 3: Fire all story images in parallel as gpt-image-1 edits.
        // SemaphoreSlim caps concurrency to avoid hitting OpenAI rate limits.
        var sem = new SemaphoreSlim(4);
        var tasks = prompts.Select(async p =>
        {
            await sem.WaitAsync();
            try { return await GenerateSingleImageWithReferenceAsync(p, characterUrl, characterBytes); }
            finally { sem.Release(); }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private async Task<string> GenerateSingleImageWithReferenceAsync(
        string prompt, string referenceImageUrl, byte[]? preloadedBytes = null)
    {
        // Use pre-downloaded bytes when available; otherwise download now.
        byte[] imageBytes = preloadedBytes ?? await _editsClient.GetByteArrayAsync(referenceImageUrl);

        const int maxAttempts = 5;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            _logger.LogInformation("PROMPT[image->gpt-image-1-edit] attempt={Attempt} len={Len}\n{Prompt}\n",
                attempt, prompt.Length, prompt);

            // MultipartFormDataContent must be rebuilt on each retry — it's read once.
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent("gpt-image-1"), "model");
            content.Add(new StringContent(prompt), "prompt");
            content.Add(new StringContent("medium"), "quality");
            content.Add(new StringContent("low"), "input_fidelity");

            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(imageContent, "image", "reference.png");

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/edits");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            req.Content = content;

            try
            {
                using var res = await _editsClient.SendAsync(req);
                var raw = await res.Content.ReadAsStringAsync();
                var status = (int)res.StatusCode;

                if (res.IsSuccessStatusCode)
                {
                    _logger.LogInformation("RESULT[image<-gpt-image-1-edit] bytes={Bytes}", raw.Length);

                    using var json = JsonDocument.Parse(raw);
                    var b64 = json.RootElement.GetProperty("data")[0].GetProperty("b64_json").GetString();
                    return "data:image/png;base64," + b64;
                }

                _logger.LogError("gpt-image-1 edits error {Status}. Attempt={Attempt} PromptLen={Len}. Body={Body}",
                    status, attempt, prompt.Length, raw);

                if (attempt < maxAttempts && IsTransient(status))
                {
                    await Task.Delay(BackoffDelayMs(attempt));
                    continue;
                }

                throw new HttpRequestException($"Images edits API failed ({status}). Body: {raw}");
            }
            catch (Exception ex) when (ex is TaskCanceledException || ex is HttpRequestException { InnerException: System.IO.IOException })
            {
                _logger.LogWarning(ex, "Image edits request failed (network/timeout). Attempt={Attempt}", attempt);

                if (attempt < maxAttempts)
                {
                    // Use a longer exponential backoff for connection-level errors — the short
                    // API backoff (~400ms) is not enough time for OpenAI's server to finish
                    // tearing down the connection and accept a fresh one.
                    await Task.Delay(NetworkBackoffDelayMs(attempt));
                    continue;
                }

                throw;
            }
        }

        throw new InvalidOperationException("GenerateSingleImageWithReferenceAsync: exhausted all attempts.");
    }

    // --- Shared helpers -------------------------------------------------------

    private static bool IsTransient(int status) => status == 429 || status >= 500;

    private static int BackoffDelayMs(int attempt) =>
        400 * attempt + Random.Shared.Next(0, 250);

    // Exponential backoff for network/connection errors: 2s, 4s, 8s, 16s, ...
    private static int NetworkBackoffDelayMs(int attempt) =>
        2000 * (1 << (attempt - 1)) + Random.Shared.Next(0, 500);

    private static bool LooksLarge(string s) => s is { Length: >= 12000 };

    private static string TrimToMaxWords(string s, int maxWords)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        ReadOnlySpan<char> span = s.AsSpan();
        int words = 0;
        bool inWord = false;
        int cutIndex = -1;

        for (int i = 0; i < span.Length; i++)
        {
            char ch = span[i];
            bool ws = char.IsWhiteSpace(ch);

            if (!ws && !inWord)
            {
                inWord = true;
                words++;
                if (words > maxWords) { cutIndex = i; break; }
            }
            else if (ws)
            {
                inWord = false;
            }
        }

        if (cutIndex < 0) return s;

        int end = cutIndex;
        while (end > 0 && !char.IsWhiteSpace(span[end - 1])) end--;
        return s[..end].TrimEnd();
    }
}
