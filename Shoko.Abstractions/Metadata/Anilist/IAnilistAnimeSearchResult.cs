using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Anilist.Enums;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Anilist;

/// <summary>
/// An Anilist anime search result.
/// </summary>
public interface IAnilistAnimeSearchResult
{
    /// <summary>
    /// Anilist Anime ID.
    /// </summary>
    int ID { get; }

    /// <summary>
    /// English title, or romaji title if English is not available.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Title in the original language (native).
    /// </summary>
    string OriginalTitle { get; }

    /// <summary>
    /// Original language code the anime was produced in.
    /// </summary>
    string OriginalLanguage { get; }

    /// <summary>
    /// Description/synopsis of the anime.
    /// </summary>
    string Overview { get; }

    /// <summary>
    /// Indicates the anime is restricted to an age group above the legal age
    /// (adult content).
    /// </summary>
    bool IsRestricted { get; }

    /// <summary>
    /// The current releasing status for the animated work.
    /// </summary>
    AnilistMediaStatus ReleasingStatus { get; }

    /// <summary>
    /// The media source for the animated work.
    /// </summary>
    AnilistMediaSource MediaSource { get; }

    /// <summary>
    /// Season of release.
    /// </summary>
    YearlySeason? Season { get; }

    /// <summary>
    /// Year of the season.
    /// </summary>
    int? SeasonYear { get; }

    /// <summary>
    /// The date the first episode aired, if known.
    /// </summary>
    PartialDateOnly? FirstAiredAt { get; }

    /// <summary>
    /// URL to the cover image.
    /// </summary>
    string? CoverImageUrl { get; }

    /// <summary>
    /// URL to the banner image.
    /// </summary>
    string? BannerImageUrl { get; }

    /// <summary>
    /// Average user rating (0-100 scale from Anilist).
    /// </summary>
    decimal UserRating { get; }

    /// <summary>
    /// Number of user votes/ratings.
    /// </summary>
    int UserVotes { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> Genres { get; }

    /// <summary>
    /// The type of anime (TV, Movie, OVA, etc.).
    /// </summary>
    AnimeType Type { get; }
}
