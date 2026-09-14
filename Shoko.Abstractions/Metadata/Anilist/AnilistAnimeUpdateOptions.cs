namespace Shoko.Abstractions.Metadata.Anilist;

/// <summary>
/// Options for updating an AniList anime.
/// </summary>
public sealed class AnilistAnimeUpdateOptions
{
    /// <summary>
    /// The AniList anime ID.
    /// </summary>
    public required int AnimeId { get; set; }

    /// <summary>
    /// Forcefully refresh the anime, even if it was recently updated.
    /// </summary>
    public bool ForceRefresh { get; set; }

    /// <summary>
    /// Download the images for the anime after the update.
    /// </summary>
    public bool DownloadImages { get; set; }

    /// <summary>
    /// Whether to fetch the characters and staff. <see langword="null"/>
    /// follows the settings.
    /// </summary>
    public bool? DownloadCharactersAndStaff { get; set; }

    /// <summary>
    /// Only fetch what the linking UI needs, the anime and its episodes,
    /// skipping the characters, staff, images and the automatic episode
    /// matching for the linked series. A quick-fetched anime is treated as
    /// not yet refreshed, so the next normal update still does the full job.
    /// </summary>
    public bool QuickRefresh { get; set; }
}
