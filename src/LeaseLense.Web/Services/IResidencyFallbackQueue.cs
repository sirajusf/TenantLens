namespace LeaseLense.Web.Services;

public interface IResidencyFallbackQueue
{
    ValueTask QueueAsync(ResidencyFallbackJob job, CancellationToken cancellationToken = default);
    ValueTask<ResidencyFallbackJob> DequeueAsync(CancellationToken cancellationToken);
}

public sealed class ResidencyFallbackJob
{
    public string Email { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public string CommunityName { get; init; } = string.Empty;
    public string PropertyTitle { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string FileHashSha256 { get; init; } = string.Empty;
    public byte[] FileBytes { get; init; } = [];
}
