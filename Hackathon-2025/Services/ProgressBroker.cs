using System.Collections.Concurrent;
using System.Threading.Channels;
using Hackathon_2025.Models;

namespace Hackathon_2025.Services;

public class ProgressBroker : IProgressBroker
{
    private class Job
    {
        public Channel<ProgressUpdate> Pipe { get; }

        public object? Result { get; set; }

        public Job()
        {
            Pipe = Channel.CreateUnbounded<ProgressUpdate>(
                new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
        }
    }

    private readonly ConcurrentDictionary<string, Job> _jobs = new();

    public string CreateJob()
    {
        var id = Guid.NewGuid().ToString("N");
        _jobs[id] = new Job();
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
            job.Pipe.Writer.TryComplete();
    }

    public void SetResult(string jobId, object result)
    {
        if (_jobs.TryGetValue(jobId, out var job))
            job.Result = result;
    }

    public object? GetResult(string jobId)
        => _jobs.TryGetValue(jobId, out var job) ? job.Result : null;
}
