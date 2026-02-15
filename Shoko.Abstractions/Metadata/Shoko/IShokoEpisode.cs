using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Anilist;
using Shoko.Abstractions.Metadata.Anilist.CrossReferences;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;
using Shoko.Abstractions.User;
using Shoko.Abstractions.UserData;

namespace Shoko.Abstractions.Metadata.Shoko;

/// <summary>
/// Shoko episode metadata.
/// </summary>
public interface IShokoEpisode : IEpisode, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// The id of the anidb episode linked to the shoko episode.
    /// </summary>
    int AnidbEpisodeID { get; }

    /// <summary>
    /// Indicates the episode is hidden by the user.
    /// </summary>
    bool IsHidden { get; }

    /// <summary>
    /// Get the shoko series info for the episode, if available.
    /// </summary>
    new IShokoSeries? Series { get; }

    /// <summary>
    /// A direct link to the anidb episode metadata.
    /// </summary>
    IAnidbEpisode AnidbEpisode { get; }

    /// <summary>
    /// A direct link to all anilist episodes linked to the shoko episode.
    /// </summary>
    IReadOnlyList<IAnilistEpisode> AnilistEpisodes { get; }

    /// <summary>
    /// All Shoko episode ↔ AniList episode cross references linked to the Shoko episode.
    /// </summary>
    IReadOnlyList<IAnilistEpisodeCrossReference> AnilistEpisodeCrossReferences { get; }

    /// <summary>
    /// A direct link to all tmdb episodes linked to the shoko episode.
    /// </summary>
    IReadOnlyList<ITmdbEpisode> TmdbEpisodes { get; }

    /// <summary>
    /// A direct link to all tmdb movies linked to the shoko episode.
    /// </summary>
    IReadOnlyList<IMovie> TmdbMovies { get; }

    /// <summary>
    /// All Shoko episode ↔ TMDB episode cross references linked to the Shoko episode.
    /// </summary>
    IReadOnlyList<ITmdbEpisodeCrossReference> TmdbEpisodeCrossReferences { get; }

    /// <summary>
    /// All Shoko episode ↔ TMDB movie cross references linked to the Shoko episode.
    /// </summary>
    IReadOnlyList<ITmdbMovieCrossReference> TmdbMovieCrossReferences { get; }

    /// <summary>
    /// All episodes linked to this shoko episode.
    /// </summary>
    IReadOnlyList<IEpisode> LinkedEpisodes { get; }

    /// <summary>
    /// All movies linked to this shoko episode.
    /// </summary>
    IReadOnlyList<IMovie> LinkedMovies { get; }

    /// <summary>
    ///   Gets the user-specific data for the Shoko episode and user.
    /// </summary>
    /// <param name="user">
    ///   The user to get the data for.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   Thrown when the <paramref name="user"/> is not stored in the database.
    /// </exception>
    /// <returns>
    ///   The user-specific data for the Shoko episode and user.
    /// </returns>
    IEpisodeUserData GetUserData(IUser user);
}
