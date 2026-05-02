using LeaseLense.Web.Services;
using Xunit;

namespace LeaseLense.Web.Tests;

public sealed class LeaseSummarizationQueueTests
{
    [Fact]
    public async Task QueueAndDequeue_RoundTripsJob()
    {
        var queue = new LeaseSummarizationQueue();
        var job = new LeaseSummarizationJobRequest
        {
            LeaseSummarizationJobId = Guid.NewGuid(),
            Email = "user@example.com",
            FileName = "lease.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 3,
            FileHashSha256 = "ABC",
            FileBytes = [1, 2, 3]
        };

        await queue.QueueAsync(job);
        var dequeued = await queue.DequeueAsync(CancellationToken.None);

        Assert.Equal(job.LeaseSummarizationJobId, dequeued.LeaseSummarizationJobId);
        Assert.Equal(job.Email, dequeued.Email);
        Assert.Equal(job.FileName, dequeued.FileName);
        Assert.Equal(job.ContentType, dequeued.ContentType);
        Assert.Equal(job.FileSizeBytes, dequeued.FileSizeBytes);
        Assert.Equal(job.FileHashSha256, dequeued.FileHashSha256);
        Assert.Equal(job.FileBytes.Length, dequeued.FileBytes.Length);
    }
}

