using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeaseLense.Web.Logging;

public sealed class ApplicationFileLoggerProvider : ILoggerProvider
{
    private readonly IOptionsMonitor<ApplicationFileLoggingOptions> _optionsMonitor;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly object _fileLock = new();

    public ApplicationFileLoggerProvider(
        IOptionsMonitor<ApplicationFileLoggingOptions> optionsMonitor,
        IHostEnvironment hostEnvironment)
    {
        _optionsMonitor = optionsMonitor;
        _hostEnvironment = hostEnvironment;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new ApplicationFileLogger(categoryName, _optionsMonitor, _hostEnvironment, _fileLock);
    }

    public void Dispose()
    {
    }
}
