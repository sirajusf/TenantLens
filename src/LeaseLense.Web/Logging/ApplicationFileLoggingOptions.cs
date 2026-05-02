using Microsoft.Extensions.Logging;

namespace LeaseLense.Web.Logging;

public sealed class ApplicationFileLoggingOptions
{
    public const string SectionName = "ApplicationFileLogging";

    public bool Enabled { get; set; } = true;

    public string RelativeDirectory { get; set; } = "logs";

    public string FileNamePrefix { get; set; } = "application";

    public LogLevel MinimumLevel { get; set; } = LogLevel.Warning;
}
