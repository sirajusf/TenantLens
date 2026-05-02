namespace LeaseLense.Web.Services;

public interface ILeaseSummarizationQueue
{
    ValueTask QueueAsync(LeaseSummarizationJobRequest job, CancellationToken cancellationToken = default);
    ValueTask<LeaseSummarizationJobRequest> DequeueAsync(CancellationToken cancellationToken);
}

public sealed class LeaseSummarizationJobRequest
{
    public Guid LeaseSummarizationJobId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string FileHashSha256 { get; init; } = string.Empty;
    public byte[] FileBytes { get; init; } = [];
}

