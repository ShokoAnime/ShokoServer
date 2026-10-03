using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   Keeps every plugin source's series, with their seasons and episodes, so
///   a provider does not have to.
/// </summary>
/// <remarks>
///   A stored series reads back whole, with what the other stores hold for
///   it. Every source but the core's own can be written; the core keeps the
///   series of <c>shoko</c>, <c>user</c>, <c>generated</c> and <c>anidb</c>
///   itself. A write raises the series, season and episode events on
///   <see cref="IMetadataService"/>.
/// </remarks>
public interface IMetadataSeriesStore
{
    #region Reading

    /// <summary>
    ///   Looks up a series.
    /// </summary>
    /// <param name="id">The series, e.g. <c>anilist://series/21</c>.</param>
    /// <returns>The series, or <c>null</c> when it is not stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    ISeries? GetSeries(MetadataGuid id);

    /// <summary>
    ///   Looks up a season.
    /// </summary>
    /// <param name="id">The season.</param>
    /// <returns>The season, or <c>null</c> when it is not stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    ISeason? GetSeason(MetadataGuid id);

    /// <summary>
    ///   Looks up an episode.
    /// </summary>
    /// <param name="id">The episode.</param>
    /// <returns>The episode, or <c>null</c> when it is not stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    IEpisode? GetEpisode(MetadataGuid id);

    /// <summary>
    ///   Every series a source has stored.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The series, in no particular order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    IReadOnlyList<ISeries> GetAllSeries(MetadataSource source);

    /// <summary>
    ///   Every season a source has stored.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The seasons, in no particular order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    IReadOnlyList<ISeason> GetAllSeasons(MetadataSource source);

    /// <summary>
    ///   Every episode a source has stored.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The episodes, in no particular order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    IReadOnlyList<IEpisode> GetAllEpisodes(MetadataSource source);

    #endregion

    #region Writing

    /// <summary>
    ///   Stores a series whole, replacing what was stored for it: its seasons
    ///   and episodes become exactly the ones given, each one's titles and
    ///   overviews exactly its own, and the series' content ratings
    ///   exactly the ones given.
    /// </summary>
    /// <remarks>
    ///   A queued job brings the episode links in step when an episode changed.
    ///   A season or episode left out takes its image links, tags, studios,
    ///   networks, cast, crew, relations and suggestions along, and an episode
    ///   also its links and hidden flag. The people, studios and networks it
    ///   named stay, so save order does not matter; unused ones are purged later.
    /// </remarks>
    /// <param name="series">The series, with its seasons and episodes.</param>
    /// <returns>How many series, seasons and episodes were added, changed or removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="series"/> is or holds <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   An ID names the wrong kind of entry, is on another source than the
    ///   series or on a source the core keeps itself, an episode names a
    ///   season the series does not have, or a code is too long.
    /// </exception>
    int SaveSeries(MetadataSeriesData series);

    /// <summary>
    ///   Removes a series with its seasons and episodes: their titles,
    ///   overviews, content ratings, image links, tags, studios, networks,
    ///   cast, crew, relations and suggestions, and every ordering of the
    ///   series, of any source, with the choice of one and the hidden flags of
    ///   its episodes. The creators, characters, studios and networks stay.
    /// </summary>
    /// <param name="id">The series.</param>
    /// <returns>How many series, seasons and episodes were removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   The ID does not name a series, or is on a source the core keeps itself.
    /// </exception>
    int RemoveSeries(MetadataGuid id);

    #endregion
}
