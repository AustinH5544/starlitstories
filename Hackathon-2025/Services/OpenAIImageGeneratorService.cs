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

        foreach (var originalPrompt in prompts)
        {
            // always try full prompt first
            string workingPrompt = originalPrompt ?? string.Empty;
            bool trimmedForRetry = false;

            const int maxAttempts = 4;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var msg = BuildImagesRequest(workingPrompt);
                _logger.LogInformation("PROMPT[image->dall-e-3] attempt={Attempt} len={Len}\n{Prompt}\n",
                    attempt, workingPrompt.Length, workingPrompt);

                HttpResponseMessage? res = null;
                string? raw = null;
                string? reqId = null;
                int status = 0;

                try
                {
                    res = await _httpClient.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead);
                    res.Headers.TryGetValues("x-request-id", out var reqIds);
                    reqId = reqIds?.FirstOrDefault();
                    status = (int)res.StatusCode;

                    raw = await res.Content.ReadAsStringAsync();

                    if (res.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("RESULT[image<-dall-e-3] ReqId={ReqId} bytes={Bytes}",
                            reqId, raw?.Length ?? 0);

                        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw ?? ""));
                        using var json = await JsonDocument.ParseAsync(stream);
                        var url = json.RootElement.GetProperty("data")[0].GetProperty("url").GetString();
                        results.Add(url!);
                        break; // success for this prompt
                    }

                    // error path
                    _logger.LogError("OpenAI images error {Status}. Attempt={Attempt} ReqId={ReqId} PromptLen={Len}. Body={Body}",
                        status, attempt, reqId, workingPrompt.Length, raw);

                    if (attempt < maxAttempts && IsTransient(status))
                    {
                        // only consider trimming ON RETRY
                        if (!trimmedForRetry && LooksLarge(workingPrompt))
                        {
                            var trimmed = TrimToMaxWords(workingPrompt, 2000);
                            if (!string.Equals(trimmed, workingPrompt, StringComparison.Ordinal))
                            {
                                _logger.LogWarning("Retrying with trimmed prompt (words≈2000). OldLen={OldLen} NewLen={NewLen} ReqId={ReqId}",
                                    workingPrompt.Length, trimmed.Length, reqId);
                                workingPrompt = trimmed;
                                trimmedForRetry = true;
                            }
                        }

                        await Task.Delay(BackoffDelayMs(attempt));
                        continue;
                    }

                    // non-transient or out of retries
                    throw new HttpRequestException($"Images API failed ({status}). ReqId={reqId}. Body: {raw}");
                }
                catch (TaskCanceledException ex)
                {
                    // treat timeouts as transient
                    _logger.LogWarning(ex, "Images request timed out. Attempt={Attempt}", attempt);

                    if (attempt < maxAttempts)
                    {
                        if (!trimmedForRetry && LooksLarge(workingPrompt))
                        {
                            var trimmed = TrimToMaxWords(workingPrompt, 2000);
                            if (!string.Equals(trimmed, workingPrompt, StringComparison.Ordinal))
                            {
                                _logger.LogWarning("Retrying (timeout) with trimmed prompt (words≈2000). OldLen={OldLen} NewLen={NewLen}",
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
        }

        return results;
    }

    private HttpRequestMessage BuildImagesRequest(string prompt)
    {
        var body = new
        {
            model = "dall-e-3",
            prompt,
            n = 1,
            size = "1024x1024"
        };

        var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/generations");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        msg.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return msg;
    }

    private static bool IsTransient(int status) => status == 429 || status >= 500;

    private static int BackoffDelayMs(int attempt)
    {
        // 400ms, 800ms, 1200ms … + jitter up to 250ms
        return 400 * attempt + Random.Shared.Next(0, 250);
    }

    // quick heuristic: if the prompt is quite long (likely > ~2000 words), consider trimming on retry
    // ~6 chars per word average -> 2000 words ~ 12k chars
    private static bool LooksLarge(string s) => s is { Length: >= 12000 };

    // single-pass, allocation-free(ish) word-cap that keeps whole words
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
                if (words > maxWords)
                {
                    cutIndex = i; // start of the (maxWords+1)-th word
                    break;
                }
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