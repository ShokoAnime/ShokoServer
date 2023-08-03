using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.Settings;

public class AVDumpSettings
{
    /// <summary>
    /// Max concurrent hashing jobs to run in AVDump at the same time.
    /// </summary>
    [Range(0, 64)]
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Sets the the timeout for a creq upload before retrying or bailing.
    /// </summary>
    [Range(20, 300)]
    public int CreqTimeout { get; set; } = 20;

    /// <summary>
    /// Sets the max retry attempts for a creq upload before bailing.
    /// </summary>
    [Range(1, 20)]
    public int CreqMaxRetries { get; set; } = 3;
}
