namespace LeaseLense.Web.Services;

public sealed class LlmFoundryFileLoggingOptions
{
    public const string SectionName = "LlmFoundryFileLogging";

    public bool Enabled { get; set; } = true;

    /// <summary>Directory under the web content root (e.g. "logs").</summary>
    public string RelativeDirectory { get; set; } = "logs";

    public string FileNamePrefix { get; set; } = "llm-foundry";

    /// <summary>Max length for response/error body excerpts in the log (characters).</summary>
    public int ExcerptMaxLength { get; set; } = 2_000;

    /// <summary>Max length for exception stack traces (characters).</summary>
    public int StackTraceMaxLength { get; set; } = 4_000;
}
