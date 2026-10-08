using System.Collections.Concurrent;
using Hackathon_2025.Models;
using Hackathon_2025.Services;

namespace Hackathon_2025.Tests.Utils;

/// <summary>Stands in for the OpenAI-backed story generator. Returns a canned two-page story instantly.</summary>
internal sealed class FakeStoryGenerator : IStoryGeneratorService
{
    private int _callCount;

    public Exception? ThrowOnGenerate { get; set; }

    /// <summary>When set, generation pauses until this completes (lets a test change things mid-generation).</summary>
    public TaskCompletionSource? Hold { get; set; }

    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when generation has been called at least once.</summary>
    public Task Started => _started.Task;
    public StoryRequest? LastRequest { get; private set; }
    public int CallCount => _callCount;

    public async Task<StoryResult> GenerateFullStoryAsync(StoryRequest request, Action<ProgressUpdate>? onProgress = null)
    {
        LastRequest = request;
        Interlocked.Increment(ref _callCount);
        _started.TrySetResult();

        if (Hold is not null)
            await Hold.Task;

        if (ThrowOnGenerate is not null)
            throw ThrowOnGenerate;

        onProgress?.Invoke(new ProgressUpdate { Stage = "text", Percent = 50, Message = "Writing..." });

        return new StoryResult
        {
            Title = "Fake Story",
            CoverImagePrompt = "cover prompt",
            CoverImageUrl = "https://img.test/cover.png",
            Pages = new List<StoryPageDto>
            {
                new("Page one text", "page one prompt", "https://img.test/1.png"),
                new("Page two text", "page two prompt", "https://img.test/2.png")
            }
        };
    }
}

/// <summary>Stands in for Azure Blob Storage. Records file names and returns deterministic URLs.</summary>
internal sealed class FakeBlobUploadService : IBlobUploadService
{
    public ConcurrentBag<string> Uploaded { get; } = new();

    /// <summary>When set, uploads throw this (simulates Blob Storage being unavailable).</summary>
    public Exception? ThrowOnUpload { get; set; }

    // Completes synchronously on purpose: this is the harshest timing for callers that upload pages in parallel.
    public Task<string> UploadImageAsync(string imageUrl, string fileName)
    {
        if (ThrowOnUpload is not null)
            return Task.FromException<string>(ThrowOnUpload);
        Uploaded.Add(fileName);
        return Task.FromResult($"https://blob.test/{fileName}");
    }

    public Task<string> UploadBase64ImageAsync(string base64Data, string fileName)
    {
        Uploaded.Add(fileName);
        return Task.FromResult($"https://blob.test/{fileName}");
    }

    public ConcurrentBag<string> Deleted { get; } = new();
    public Exception? ThrowOnDelete { get; set; }

    public Task DeleteByUrlAsync(string blobUrl)
    {
        if (ThrowOnDelete is not null)
            throw ThrowOnDelete;
        Deleted.Add(blobUrl);
        return Task.CompletedTask;
    }
}
