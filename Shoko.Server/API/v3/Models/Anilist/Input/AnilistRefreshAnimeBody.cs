using System.ComponentModel;

#nullable enable
namespace Shoko.Server.API.v3.Models.Anilist.Input;

/// <summary>
/// Options for refreshing an Anilist anime. Mirrors the TMDB show refresh body.
/// </summary>
public class AnilistRefreshAnimeBody
{
    /// <summary>
    /// Forcefully refresh even if the anime was recently updated.
    /// </summary>
    public bool Force { get; set; } = false;

    /// <summary>
    /// Download the images after the update.
    /// </summary>
    [DefaultValue(true)]
    public bool DownloadImages { get; set; } = true;

    /// <summary>
    /// Fetch the characters and staff. <see langword="null"/> follows the settings.
    /// </summary>
    public bool? DownloadCharactersAndStaff { get; set; } = null;

    /// <summary>
    /// Wait for the refresh to complete before returning.
    /// </summary>
    public bool Immediate { get; set; } = false;

    /// <summary>
    /// Only fetch the anime and its episodes, for the linking UI. Only
    /// meaningful together with <see cref="Immediate"/>; a queued refresh
    /// always does the full job.
    /// </summary>
    public bool QuickRefresh { get; set; } = false;
}
