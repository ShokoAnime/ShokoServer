using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Settings;

/// <summary>
/// What the core does with every plugin metadata source that has no value of
/// its own in <see cref="MetadataSettings.Sources"/>.
/// </summary>
public class MetadataSourceDefaults
{
    /// <summary>
    /// Number of days after today an AniDB episode may air and still be
    /// matched to a source's episode, counted from its earliest showing. Set
    /// to <c>0</c> to match only what aired by today.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Large)]
    [Display(Name = "Match Episodes Airing Within (days)")]
    [EnvironmentVariable("METADATA_EPISODE_MATCH_LOOK_AHEAD_DAYS")]
    [Range(0, 365)]
    [DefaultValue(7)]
    public int EpisodeMatchLookAheadDays { get; set; } = 7;

    /// <summary>
    /// Which images to download, per image type.
    /// </summary>
    [Display(Name = "Images")]
    public MetadataImageSettings Images { get; set; } = new();
}

/// <summary>
/// What the core does with one plugin metadata source in place of the
/// shared defaults. A value left empty uses the default.
/// </summary>
public class MetadataSourceOverrides
{
    /// <summary>
    /// The source these settings are for.
    /// </summary>
    [Key]
    [Display(Name = "Source")]
    [Required]
    public MetadataSource Source { get; set; } = MetadataSource.User;

    /// <summary>
    /// Number of days after today an AniDB episode may air and still be
    /// matched to one of the source's episodes, or empty to use the default.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Large)]
    [Display(Name = "Match Episodes Airing Within (days)")]
    [Range(0, 365)]
    public int? EpisodeMatchLookAheadDays { get; set; }

    /// <summary>
    /// Which images to download for the source, per image type, or empty to
    /// use the default.
    /// </summary>
    [Display(Name = "Images")]
    public MetadataImageSettings? Images { get; set; }

    /// <summary>
    /// Refuses a core source, which the core handles itself.
    /// </summary>
    /// <param name="config">The settings to check.</param>
    /// <returns>The errors, by member name.</returns>
    [ConfigurationAction(ConfigurationActionType.Validate)]
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(MetadataSourceOverrides config)
    {
        var errors = new Dictionary<string, IReadOnlyList<string>>();
        if (config.Source.IsCore)
            errors.Add(nameof(config.Source), [$"{nameof(config.Source)} must be a plugin source; the core handles {config.Source.Value} itself."]);
        return errors;
    }
}
