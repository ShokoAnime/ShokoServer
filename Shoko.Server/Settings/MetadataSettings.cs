using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Server.Settings;

/// <summary>
/// Settings for the metadata the plugin providers keep in the core's stores.
/// </summary>
public class MetadataSettings
{
    /// <summary>
    /// Number of days a creator, character, studio or network of a plugin
    /// source, or a person or network of TMDB, may go unused before it is
    /// purged. Purged daily, and when the purge action is run.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Large)]
    [Display(Name = "Purge Orphaned Metadata After (days)")]
    [EnvironmentVariable("METADATA_PURGE_ORPHANED_AFTER_DAYS")]
    [Range(1, 365)]
    [DefaultValue(7)]
    public int PurgeOrphanedAfterDays { get; set; } = 7;

    /// <summary>
    /// Number of days a series, movie or collection of TMDB or a plugin source
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
}
