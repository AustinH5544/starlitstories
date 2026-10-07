using Hackathon_2025.Models;

namespace Hackathon_2025.Services;

public interface IProgressBroker
{
    string CreateJob(int ownerUserId);
    void Publish(string jobId, ProgressUpdate update);
    IAsyncEnumerable<ProgressUpdate> Consume(string jobId, CancellationToken ct);
    void Complete(string jobId);
    void SetResult(string jobId, object result);
    // Returns null when the job is unknown, unfinished, or owned by a different user.
    object? GetResult(string jobId, int requestingUserId);
}
