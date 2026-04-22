using LeaseLense.Web.Services;
using Xunit;

namespace LeaseLense.Web.Tests;

public sealed class ResidencyFallbackQueueTests
{
    [Fact]
    public async Task QueueAndDequeue_RoundTripsJob()
    {
        var queue = new ResidencyFallbackQueue();
        var job = new ResidencyFallbackJob
        {
            Email = "user@example.com",
            DocumentType = "lease",
            FileName = "lease.pdf",
            FileBytes = [1, 2, 3]
        };

        await queue.QueueAsync(job);
        var dequeued = await queue.DequeueAsync(CancellationToken.None);

        Assert.Equal(job.Email, dequeued.Email);
        Assert.Equal(job.DocumentType, dequeued.DocumentType);
        Assert.Equal(job.FileName, dequeued.FileName);
        Assert.Equal(job.FileBytes.Length, dequeued.FileBytes.Length);
    }
}
