using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeaseLense.Web.Logging;

internal sealed class ApplicationFileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly IOptionsMonitor<ApplicationFileLoggingOptions> _optionsMonitor;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly object _fileLock;

    public ApplicationFileLogger(
        string categoryName,
        IOptionsMonitor<ApplicationFileLoggingOptions> optionsMonitor,
        IHostEnvironment hostEnvironment,
        object fileLock)
    {
        _categoryName = categoryName;
        _optionsMonitor = optionsMonitor;
        _hostEnvironment = hostEnvironment;
        _fileLock = fileLock;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        var options = _optionsMonitor.CurrentValue;
        return options.Enabled && logLevel >= options.MinimumLevel;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message;
        try
        {
            message = formatter(state, exception);
        }
        catch
        {
            message = state?.ToString() ?? string.Empty;
        }

        var logDirectory = ResolveLogDirectoryPath(_optionsMonitor.CurrentValue, _hostEnvironment);
        Directory.CreateDirectory(logDirectory);

        var timestamp = DateTimeOffset.UtcNow;
        var filePath = Path.Combine(
            logDirectory,
            $"{SanitizeFileNamePrefix(_optionsMonitor.CurrentValue.FileNamePrefix)}-{timestamp:yyyyMMdd}.log");

        var builder = new StringBuilder();
        builder.Append('[')
            .Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff 'UTC'"))
            .Append("] [")
            .Append(logLevel)
            .Append("] ")
            .Append(_categoryName);

        if (eventId.Id != 0 || !string.IsNullOrWhiteSpace(eventId.Name))
        {
            builder.Append(" (EventId: ").Append(eventId.Id);
            if (!string.IsNullOrWhiteSpace(eventId.Name))
            {
                builder.Append(", ").Append(eventId.Name);
            }

            builder.Append(')');
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            builder.Append(": ").Append(message);
        }

        builder.AppendLine();

        if (exception is not null)
        {
            builder.AppendLine(exception.ToString());
        }

        lock (_fileLock)
        {
            File.AppendAllText(filePath, builder.ToString());
        }
    }

    private static string ResolveLogDirectoryPath(
        ApplicationFileLoggingOptions options,
        IHostEnvironment hostEnvironment)
    {
        var contentRoot = Path.GetFullPath(hostEnvironment.ContentRootPath);
        var relativeDirectory = (options.RelativeDirectory ?? "logs").Trim();
        if (string.IsNullOrWhiteSpace(relativeDirectory) || relativeDirectory.Contains("..", StringComparison.Ordinal))
        {
            relativeDirectory = "logs";
        }

        var target = Path.GetFullPath(Path.Combine(contentRoot, relativeDirectory));
        return IsSubPathOrSame(contentRoot, target) ? target : Path.Combine(contentRoot, "logs");
    }

    private static bool IsSubPathOrSame(string baseDirectory, string candidate)
    {
        var normalizedBase = Path.GetFullPath(baseDirectory);
        if (normalizedBase[^1] != Path.DirectorySeparatorChar && normalizedBase[^1] != Path.AltDirectorySeparatorChar)
        {
            normalizedBase += Path.DirectorySeparatorChar;
        }

        var normalizedCandidate = Path.GetFullPath(candidate);
        return normalizedCandidate.Equals(Path.GetFullPath(baseDirectory), StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileNamePrefix(string? prefix)
    {
        var value = prefix?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return "application";
        }

        return value;
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
