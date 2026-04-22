using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeaseLense.Web.Services;

public sealed class LlmFoundryErrorFileLog : ILlmFoundryErrorFileLog
{
    private static readonly JsonSerializerOptions JsonLineOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IOptionsMonitor<LlmFoundryFileLoggingOptions> _optionsMonitor;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<LlmFoundryErrorFileLog> _logger;
    private readonly object _fileLock = new();
    private int _fileWriteFailureCount;

    public LlmFoundryErrorFileLog(
        IOptionsMonitor<LlmFoundryFileLoggingOptions> optionsMonitor,
        IHostEnvironment hostEnvironment,
        ILogger<LlmFoundryErrorFileLog> logger)
    {
        _optionsMonitor = optionsMonitor;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public void Write(
        LlmFoundryFileLogSeverity severity,
        string eventType,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, string?>? context = null)
    {
        var eventTypeForDiagnostics = eventType;
        try
        {
            var options = _optionsMonitor.CurrentValue;
            if (!options.Enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(eventType))
            {
                eventType = "Unspecified";
            }

            if (string.IsNullOrEmpty(message) && exception is not null)
            {
                message = exception.Message;
            }

            if (string.IsNullOrEmpty(message))
            {
                message = "No message.";
            }

            var now = DateTime.UtcNow;
            var line = new
            {
                timestampUtc = now,
                machine = Environment.MachineName,
                processId = Environment.ProcessId,
                severity = severity.ToString(),
                eventType,
                message,
                exception = SerializeException(exception, options),
                context = context is null
                    ? null
                    : context.ToDictionary(static kv => kv.Key, static kv => kv.Value, StringComparer.Ordinal)
            };

            var json = JsonSerializer.Serialize(line, JsonLineOptions) + Environment.NewLine;
            if (!TryAppendToDailyFile(options, now, json))
            {
                var count = Interlocked.Increment(ref _fileWriteFailureCount);
                if (count <= 3)
                {
                    try
                    {
                        _logger.LogError(
                            "Failed to write LLM Foundry error file log. EventType: {EventType}. Message: {Message}",
                            eventType,
                            TruncateForLogger(message, 500));
                    }
                    catch
                    {
                    }
                }
            }
        }
        catch (Exception ex)
        {
            try
            {
                _logger.LogWarning(
                    ex,
                    "LLM Foundry file log skipped after unexpected error. EventType: {EventType}",
                    string.IsNullOrWhiteSpace(eventTypeForDiagnostics) ? "Unspecified" : eventTypeForDiagnostics);
            }
            catch
            {
            }
        }
    }

    private static object? SerializeException(Exception? ex, LlmFoundryFileLoggingOptions options)
    {
        if (ex is null)
        {
            return null;
        }

        return new
        {
            type = ex.GetType().FullName,
            message = ex.Message,
            stackTrace = Truncate(ex.StackTrace, options.StackTraceMaxLength)
        };
    }

    private bool TryAppendToDailyFile(LlmFoundryFileLoggingOptions options, DateTime timestampUtc, string line)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_hostEnvironment.ContentRootPath))
            {
                return false;
            }

            var logDir = ResolveLogDirectoryPath(options, _hostEnvironment);
            Directory.CreateDirectory(logDir);
            var prefix = options.FileNamePrefix?.Trim();
            if (string.IsNullOrEmpty(prefix) || prefix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                prefix = "llm-foundry";
            }

            var fileName = $"{prefix}-{timestampUtc:yyyyMMdd}.log";
            var filePath = Path.Combine(logDir, fileName);
            lock (_fileLock)
            {
                File.AppendAllText(filePath, line);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveLogDirectoryPath(
        LlmFoundryFileLoggingOptions options,
        IHostEnvironment hostEnvironment)
    {
        var contentRoot = Path.GetFullPath(hostEnvironment.ContentRootPath);
        var sub = (options.RelativeDirectory ?? "logs").Trim();
        if (string.IsNullOrEmpty(sub) || sub.Contains("..", StringComparison.Ordinal))
        {
            sub = "logs";
        }

        var target = Path.GetFullPath(Path.Combine(contentRoot, sub));
        return IsSubPathOrSame(contentRoot, target) ? target : Path.Combine(contentRoot, "logs");
    }

    private static bool IsSubPathOrSame(string baseDirectory, string candidate)
    {
        var a = Path.GetFullPath(baseDirectory);
        if (a[^1] != Path.DirectorySeparatorChar && a[^1] != Path.AltDirectorySeparatorChar)
        {
            a += Path.DirectorySeparatorChar;
        }

        var b = Path.GetFullPath(candidate);
        return b.Equals(Path.GetFullPath(baseDirectory), StringComparison.OrdinalIgnoreCase)
            || b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...(truncated)";
    }

    private static string TruncateForLogger(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...(truncated)";
    }
}
