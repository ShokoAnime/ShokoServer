using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Settings;

/// <summary>
/// Settings for the metadata kept in the core's stores.
/// </summary>
public class MetadataSettings
{
    /// <summary>
    /// Number of days a creator, character, studio, network or image may go
    /// unused before it is purged. Purged daily, and the default for the purge
    /// actions.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Large)]
    [Display(Name = "Purge Orphaned Metadata After (days)")]
    [EnvironmentVariable("METADATA_PURGE_ORPHANED_AFTER_DAYS")]
    [Range(1, MaxPurgeOrphanedAfterDays)]
    [DefaultValue(7)]
    public int PurgeOrphanedAfterDays { get; set; } = 7;

    /// <summary>
    /// The most days <see cref="PurgeOrphanedAfterDays"/> takes.
    /// </summary>
    public const int MaxPurgeOrphanedAfterDays = 365;

    /// <summary>
    /// Number of days a series, movie or collection of a plugin source
    /// may stay stored with nothing linking to it before it is purged, counted
    /// from its last refresh. Purged daily. Set to <c>0</c> to never purge
    /// them on their own. <c>TMDB_AUTO_PURGE_UNLINKED_AFTER_DAYS</c> is still
    /// honoured, with a warning, while
    /// <c>METADATA_AUTO_PURGE_UNLINKED_AFTER_DAYS</c> is unset.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Large)]
    [Display(Name = "Purge Unlinked Metadata After (days)")]
    [EnvironmentVariable("METADATA_AUTO_PURGE_UNLINKED_AFTER_DAYS")]
    [Range(0, 365)]
    [DefaultValue(14)]
    public int AutoPurgeUnlinkedAfterDays { get; set; } = 14;

    /// <summary>
    /// What the core does with every plugin source that has no value of its
    /// own in <see cref="Sources"/>.
    /// </summary>
    [Display(Name = "Metadata Source Defaults")]
    public MetadataSourceDefaults SourceDefaults { get; set; } = new();

    /// <summary>
    /// What the core does with each plugin source, keyed by source, in place
    /// of <see cref="SourceDefaults"/>.
    /// </summary>
    [Display(Name = "Metadata Sources")]
    [List(ListType = DisplayListType.ComplexTab)]
    public List<MetadataSourceOverrides> Sources { get; set; } = [];

    /// <summary>
    /// How many days after today an AniDB episode may air and still be
    /// matched to one of a source's episodes: the source's own value, else
    /// the default.
    /// </summary>
    /// <param name="source">The source, or <c>null</c> for the default.</param>
    /// <returns>The days.</returns>
    public int GetEpisodeMatchLookAheadDays(MetadataSource? source)
        => GetOverrides(source)?.EpisodeMatchLookAheadDays ?? SourceDefaults.EpisodeMatchLookAheadDays;

    /// <summary>
    /// Which images to download for a source: its own settings, else the
    /// default.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The settings.</returns>
    public MetadataImageSettings GetImageSettings(MetadataSource source)
        => GetOverrides(source)?.Images ?? SourceDefaults.Images;

    /// <summary>
    /// The source's own entry in <see cref="Sources"/>, if it has one.
    /// </summary>
    /// <param name="source">The source, or <c>null</c>.</param>
    /// <returns>The entry, or <c>null</c>.</returns>
    private MetadataSourceOverrides? GetOverrides(MetadataSource? source)
        => source is null ? null : Sources.FirstOrDefault(overrides => overrides.Source == source);
}
