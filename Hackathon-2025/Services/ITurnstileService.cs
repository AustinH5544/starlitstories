namespace Hackathon_2025.Services;

public interface ITurnstileService
{
    Task<TurnstileVerificationResult> VerifyAsync(
        string? token,
        string? remoteIp,
        CancellationToken cancellationToken = default);
}

public sealed record TurnstileVerificationResult(bool Success, string? ErrorMessage = null)
{
    public static TurnstileVerificationResult Passed() => new(true);

    public static TurnstileVerificationResult Failed(string message) => new(false, message);
}
