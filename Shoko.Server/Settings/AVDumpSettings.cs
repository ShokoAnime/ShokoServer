using System.ComponentModel.DataAnnotations;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Config.Enums;

namespace Shoko.Server.Settings;

public class AVDumpSettings
{
    /// <summary>
    /// Max concurrent hashing jobs to run in AVDump at the same time.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Advanced = true)]
    [Range(0, 64)]
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Sets the the timeout for a creq upload before retrying or bailing.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Advanced = true)]
    [Range(20, 300)]
    public int CreqTimeout { get; set; } = 20;

    /// <summary>
    /// Sets the max retry attempts for a creq upload before bailing.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Advanced = true)]
    [Range(1, 20)]
    public int CreqMaxRetries { get; set; } = 3;
}
