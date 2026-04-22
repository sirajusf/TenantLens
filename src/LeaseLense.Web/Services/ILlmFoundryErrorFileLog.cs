namespace LeaseLense.Web.Services;

public enum LlmFoundryFileLogSeverity
{
    Warning,
    Error
}

public interface ILlmFoundryErrorFileLog
{
    /// <summary>Best-effort only; implementations must not throw to callers.</summary>
    void Write(
        LlmFoundryFileLogSeverity severity,
        string eventType,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, string?>? context = null);
}

public sealed class NullLlmFoundryErrorFileLog : ILlmFoundryErrorFileLog
{
    public static readonly NullLlmFoundryErrorFileLog Instance = new();

    public void Write(
        LlmFoundryFileLogSeverity severity,
        string eventType,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, string?>? context = null)
    {
    }
}
