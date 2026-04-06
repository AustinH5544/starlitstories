namespace Hackathon_2025.Services;

public sealed class SuspiciousImageGenerationException : Exception
{
    public SuspiciousImageGenerationException(string message)
        : base(message)
    {
    }
}
