using System.ComponentModel.DataAnnotations;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Server.Renamer;

public class WebAOMSettings
{
    /// <summary>
    /// The maximum length to truncate episode names to.
    /// </summary>
    [RenamerSetting(Name = "Max Episode Length", Description = "The maximum length to truncate episode names to.")]
    [Range(1, 250)]
    public int MaxEpisodeLength { get; set; } = 33;

    /// <summary>
    /// Whether to skip disk space checks.
    /// </summary>
    [RenamerSetting(Name = "Skip Disk Space Checks", Description = "Whether to skip disk space checks.")]
    public bool SkipDiskSpaceChecks { get; set; }

    /// <summary>
    /// Whether to place files in a folder structure based on the Shoko group structure.
    /// </summary>
    [RenamerSetting(Name = "Group Aware Sorting", Description = "Whether to place files in a folder structure based on the Shoko group structure.")]
    public bool GroupAwareSorting { get; set; }

    /// <summary>
    /// WebAOM Script.
    /// </summary>
    [RenamerSetting(Type = RenamerSettingType.Code, Language = CodeLanguage.PlainText, Description = "WebAOM Script.")]
    public string Script { get; set; }
}
