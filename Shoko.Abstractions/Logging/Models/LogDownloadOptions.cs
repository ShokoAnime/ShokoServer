using Shoko.Abstractions.Logging.Services;

namespace Shoko.Abstractions.Logging.Models;

/// <summary>
///   Options for <see cref="ILogService.DownloadLogFile"/> and
///   <see cref="ILogService.DownloadRange"/>.
/// </summary>
public sealed class LogDownloadOptions : LogBaseOptions
{
    /// <summary>
    ///   Output format. Default <see cref="LogSerializeFormat.Simple"/>.
    /// </summary>
    public LogSerializeFormat Format { get; set; } = LogSerializeFormat.Simple;
}
