using System.Net.Http.Json;
using Hackathon_2025.Options;
using Microsoft.Extensions.Options;

namespace Hackathon_2025.Services;

public sealed class TurnstileService : ITurnstileService
{
    private const string VerifyEndpoint = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<TurnstileOptions> _options;
    private readonly ILogger<TurnstileService> _logger;

    public TurnstileService(
        IHttpClientFactory httpClientFactory,
        IOptions<TurnstileOptions> options,
        ILogger<TurnstileService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<TurnstileVerificationResult> VerifyAsync(
        string? token,
        string? remoteIp,
        CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (!options.Enabled)
            return TurnstileVerificationResult.Passed();

        if (string.IsNullOrWhiteSpace(token))
            return TurnstileVerificationResult.Failed("Please complete the human verification challenge.");

        try
        {
            var formData = new Dictionary<string, string>
            {
                ["secret"] = options.SecretKey,
                ["response"] = token
            };

            if (!string.IsNullOrWhiteSpace(remoteIp))
                formData["remoteip"] = remoteIp;

            using var request = new HttpRequestMessage(HttpMethod.Post, VerifyEndpoint)
            {
                Content = new FormUrlEncodedContent(formData)
            };

            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Turnstile verification failed with HTTP {StatusCode}", response.StatusCode);
                return TurnstileVerificationResult.Failed("Human verification is temporarily unavailable. Please try again.");
            }

            var payload = await response.Content.ReadFromJsonAsync<TurnstileSiteVerifyResponse>(cancellationToken);
            if (payload?.Success == true)
                return TurnstileVerificationResult.Passed();

            _logger.LogWarning(
                "Turnstile rejected signup. Codes: {ErrorCodes}",
                payload?.ErrorCodes is { Count: > 0 } ? string.Join(", ", payload.ErrorCodes) : "none");

            return TurnstileVerificationResult.Failed("Human verification failed. Please try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Turnstile verification threw an exception");
            return TurnstileVerificationResult.Failed("Human verification is temporarily unavailable. Please try again.");
        }
    }

    private sealed class TurnstileSiteVerifyResponse
    {
        public bool Success { get; init; }

        public List<string>? ErrorCodes { get; init; }
    }
}
