using System.Collections.Concurrent;
using System.Threading.Channels;
using Hackathon_2025.Models;

namespace Hackathon_2025.Services;

public class ProgressBroker : IProgressBroker
{
    // Finished jobs are kept long enough for the client's result fetch/poll, then dropped.
    public static readonly TimeSpan CompletedJobRetention = TimeSpan.FromHours(1);
    // Backstop for jobs that never reach Complete().
    public static readonly TimeSpan MaxJobAge = TimeSpan.FromHours(24);

    private class Job
    {
        public Channel<ProgressUpdate> Pipe { get; }

        public object? Result { get; set; }

        public int OwnerUserId { get; }

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset? CompletedAt { get; set; }

        public Job(int ownerUserId, DateTimeOffset createdAt)
        {
            OwnerUserId = ownerUserId;
            CreatedAt = createdAt;
            Pipe = Channel.CreateUnbounded<ProgressUpdate>(
                new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
        }
    }

    private readonly ConcurrentDictionary<string, Job> _jobs = new();
    private readonly TimeProvider _time;

    public ProgressBroker() : this(TimeProvider.System) { }

    public ProgressBroker(TimeProvider time)
    {
        _time = time;
    }

    public string CreateJob(int ownerUserId)
    {
        RemoveExpiredJobs();

        var id = Guid.NewGuid().ToString("N");
        _jobs[id] = new Job(ownerUserId, _time.GetUtcNow());
        return id;
    }

    public void Publish(string jobId, ProgressUpdate update)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Pipe.Writer.TryWrite(update);
        }
    }

    public async IAsyncEnumerable<ProgressUpdate> Consume(string jobId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
            yield break;

        var reader = job.Pipe.Reader;

        while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            while (reader.TryRead(out var item))
            {
                yield return item;
                if (item.Done) yield break;
            }
        }
    }

    public void Complete(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Pipe.Writer.TryComplete();
            job.CompletedAt ??= _time.GetUtcNow();
        }
    }

    public void SetResult(string jobId, object result)
    {
        if (_jobs.TryGetValue(jobId, out var job))
            job.Result = result;
    }

    public object? GetResult(string jobId, int requestingUserId)
        => _jobs.TryGetValue(jobId, out var job) && job.OwnerUserId == requestingUserId
            ? job.Result
            : null;

    private void RemoveExpiredJobs()
    {
        var now = _time.GetUtcNow();
        foreach (var (id, job) in _jobs)
        {
            var finishedLongAgo = job.CompletedAt is { } done && now - done > CompletedJobRetention;
            var tooOld = now - job.CreatedAt > MaxJobAge;
            if (finishedLongAgo || tooOld)
                _jobs.TryRemove(id, out _);
        }
    }
}
