
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Anilist;

/// <summary>
/// An AniList anime.
/// </summary>
public interface IAnilistAnime : ISeries, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// My Anime List (MAL) IDs linked to the AniList anime.
    /// </summary>
    IReadOnlyList<int> MalIDs { get; }

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
    /// All tags for the AniList anime.
    /// </summary>
    IReadOnlyList<IAnilistTagForAnime> Tags { get; }

    /// <summary>
    /// All known fake "seasons" for the AniList anime.
    /// </summary>
    new IReadOnlyList<IAnilistSeason> Seasons { get; }

    /// <summary>
    /// All episodes for the the AniList anime.
    /// </summary>
    new IReadOnlyList<IAnilistEpisode> Episodes { get; }
}
