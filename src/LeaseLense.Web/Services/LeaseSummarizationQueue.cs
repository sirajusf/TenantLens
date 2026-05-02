using System.Threading.Channels;

namespace LeaseLense.Web.Services;

public sealed class LeaseSummarizationQueue : ILeaseSummarizationQueue
{
    private readonly Channel<LeaseSummarizationJobRequest> _channel = Channel.CreateUnbounded<LeaseSummarizationJobRequest>();

    public ValueTask QueueAsync(LeaseSummarizationJobRequest job, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(job, cancellationToken);
    }

    public ValueTask<LeaseSummarizationJobRequest> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}

