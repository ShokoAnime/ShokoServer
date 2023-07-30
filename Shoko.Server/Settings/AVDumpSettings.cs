
using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.Settings;

public class AVDumpSettings
{
    /// <summary>
    /// Max concurrent hashing jobs to run in AVDump at the same time.
    /// </summary>
    [Range(0, 64)]
    public int MaxConcurrency { get; set; } = 1;
}
