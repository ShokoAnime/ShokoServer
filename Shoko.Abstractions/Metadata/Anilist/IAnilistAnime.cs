
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Anilist.Enums;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Anilist;

/// <summary>
/// An AniList anime.
/// </summary>
public interface IAnilistAnime : ISeries, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    ///   The ID of the My Anime List (MAL) entry linked to the AniList anime,
    ///   if set.
    /// </summary>
    int? MalID { get; }

    /// <summary>
    /// The original language the AniList anime was shot in.
    /// </summary>
    string OriginalLanguageCode { get; }

    /// <summary>
    /// The AniList anime's popularity rank.
    /// </summary>
    int Popularity { get; }

    /// <summary>
    /// The number of users that have liked the AniList anime.
    /// </summary>
    int FavoriteCount { get; }

    /// <summary>
    /// Whether the AniList anime is licensed in English.
    /// </summary>
    bool IsLicensed { get; }

    /// <summary>
    /// The AniList anime's primary color.
    /// </summary>
    string Color { get; }

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
    /// All genres for the AniList anime.
    /// </summary>
    IReadOnlyList<string> Genres { get; }

    /// <summary>
    /// All tags for the AniList anime.
    /// </summary>
    IReadOnlyList<IAnilistTagForAnime> Tags { get; }

    /// <summary>
    /// All known fake "seasons" for the AniList anime.
    /// </summary>
    new IReadOnlyList<IAnilistSeason> Seasons { get; }

    /// <summary>
    /// All episodes for the AniList anime.
    /// </summary>
    new IReadOnlyList<IAnilistEpisode> Episodes { get; }

    /// <summary>
    /// The anime AniList's users recommend to someone who liked this one, best
    /// scored first.
    /// </summary>
    new IReadOnlyList<IAnilistSuggestion> Suggestions { get; }

    /// <summary>
    /// The anime AniList's users recommend this one from.
    /// </summary>
    new IReadOnlyList<IAnilistSuggestion> SuggestedBy { get; }
}
