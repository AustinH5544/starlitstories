using Hackathon_2025.Models;

namespace Hackathon_2025.Services;

public interface IProgressBroker
{
    string CreateJob();
    void Publish(string jobId, ProgressUpdate update);
    IAsyncEnumerable<ProgressUpdate> Consume(string jobId, CancellationToken ct);
    void Complete(string jobId);
    void SetResult(string jobId, object result);
    object? GetResult(string jobId);
}
