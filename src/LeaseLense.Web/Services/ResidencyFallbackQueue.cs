using System.Threading.Channels;

namespace LeaseLense.Web.Services;

public sealed class ResidencyFallbackQueue : IResidencyFallbackQueue
{
    private readonly Channel<ResidencyFallbackJob> _channel = Channel.CreateUnbounded<ResidencyFallbackJob>();

    public ValueTask QueueAsync(ResidencyFallbackJob job, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(job, cancellationToken);
    }

    public ValueTask<ResidencyFallbackJob> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
