using System;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.TMDB;

namespace Shoko.Server.Services;

/// <summary>
///   Reads and writes the ordering chosen for each series and the hidden flag
///   of each episode, kept on the series' and episodes' own rows, so they go
///   with the rows.
/// </summary>
public interface IOrderingRowState
{
    #region Series

    /// <summary>
    ///   Whether a series has a row to keep its chosen ordering on: a Shoko
    ///   series, an AniDB anime, a TMDB show, or a plugin source's series in
    ///   the series store.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    /// <returns><c>true</c> if the series is stored.</returns>
    bool HasSeries(MetadataGuid seriesID);

    /// <summary>
    ///   The ordering chosen for a series.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    /// <returns>The ordering, or <c>null</c> when none is chosen or the series is not stored.</returns>
    MetadataGuid? GetPreferredOrdering(MetadataGuid seriesID);

    /// <summary>
    ///   Chooses an ordering for a series.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    /// <param name="orderingID">The ordering, or <c>null</c> for the series' default one.</param>
    /// <returns><c>true</c> if the choice changed; <c>false</c> when it stays or the series is not stored.</returns>
    bool SetPreferredOrdering(MetadataGuid seriesID, MetadataGuid? orderingID);

    #endregion

    #region Episodes

    /// <summary>
    ///   Whether an episode has a row to keep its hidden flag on: an AniDB or
    ///   TMDB episode, or a plugin source's episode in the series store. A
    ///   Shoko episode's flag is left to the ordering service, which updates
    ///   the stats with it.
    /// </summary>
    /// <param name="episodeID">The episode.</param>
    /// <returns><c>true</c> if the episode is stored.</returns>
    bool HasEpisode(MetadataGuid episodeID);

    /// <summary>
    ///   Whether a user hid an episode.
    /// </summary>
    /// <param name="episodeID">The episode.</param>
    /// <returns><c>true</c> if it is hidden; <c>false</c> when it is shown or not stored.</returns>
    bool IsHidden(MetadataGuid episodeID);

    /// <summary>
    ///   Hides or shows an episode.
    /// </summary>
    /// <param name="episodeID">The episode.</param>
    /// <param name="hidden">Whether to hide it.</param>
    /// <returns><c>true</c> if the flag changed; <c>false</c> when it stays or the episode is not stored.</returns>
    bool SetHidden(MetadataGuid episodeID, bool hidden);

    #endregion
}

/// <summary>
///   Keeps the chosen ordering and the hidden flags on the rows of the
///   core's sources and of the series store.
/// </summary>
/// <param name="shokoSeriesRepository">The Shoko series.</param>
/// <param name="anidbAnimeRepository">The AniDB anime.</param>
/// <param name="anidbEpisodeRepository">The AniDB episodes.</param>
/// <param name="tmdbShowRepository">The TMDB shows.</param>
/// <param name="tmdbEpisodeRepository">The TMDB episodes.</param>
/// <param name="seriesStore">Writes a plugin source's series and episodes under its own lock.</param>
public class OrderingRowState(
    AnimeSeriesRepository shokoSeriesRepository,
    AniDB_AnimeRepository anidbAnimeRepository,
    AniDB_EpisodeRepository anidbEpisodeRepository,
    TMDB_ShowRepository tmdbShowRepository,
    TMDB_EpisodeRepository tmdbEpisodeRepository,
    Lazy<MetadataSeriesStore> seriesStore
) : IOrderingRowState
{
    #region Series

    /// <inheritdoc />
    public bool HasSeries(MetadataGuid seriesID)
        => Series(seriesID).Found;

    /// <inheritdoc />
    public MetadataGuid? GetPreferredOrdering(MetadataGuid seriesID)
        => Series(seriesID).PreferredOrderingID;

    /// <inheritdoc />
    public bool SetPreferredOrdering(MetadataGuid seriesID, MetadataGuid? orderingID)
    {
        ArgumentNullException.ThrowIfNull(seriesID);
        if (seriesID.EntityType != MetadataEntityType.Series)
            return false;

        if (seriesID.Source == MetadataSource.Shoko)
        {
            if (!seriesID.TryGetNumericID<int>(out var id) || shokoSeriesRepository.GetByID(id) is not { } series || series.PreferredOrderingID == orderingID)
                return false;

            series.PreferredOrderingID = orderingID;
            shokoSeriesRepository.Save(series, false);
            return true;
        }

        if (seriesID.Source == MetadataSource.AniDB)
        {
            if (!seriesID.TryGetNumericID<int>(out var id) || anidbAnimeRepository.GetByAnimeID(id) is not { } anime || anime.PreferredOrderingID == orderingID)
                return false;

            anime.PreferredOrderingID = orderingID;
            anidbAnimeRepository.Save(anime);
            return true;
        }

        if (seriesID.Source == MetadataSource.TMDB)
        {
            if (!seriesID.TryGetNumericID<int>(out var id) || tmdbShowRepository.GetByTmdbShowID(id) is not { } show || show.PreferredOrderingID == orderingID)
                return false;

            show.PreferredOrderingID = orderingID;
            tmdbShowRepository.Save(show);
            return true;
        }

        return !seriesID.Source.IsCore && seriesStore.Value.SetPreferredOrdering(seriesID, orderingID);
    }

    /// <summary>
    ///   Finds a series' row and reads its chosen ordering.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    /// <returns>Whether the row was found, and the ordering chosen on it.</returns>
    private (bool Found, MetadataGuid? PreferredOrderingID) Series(MetadataGuid seriesID)
    {
        ArgumentNullException.ThrowIfNull(seriesID);
        if (seriesID.EntityType != MetadataEntityType.Series)
            return (false, null);

        if (seriesID.Source == MetadataSource.Shoko)
            return seriesID.TryGetNumericID<int>(out var id) && shokoSeriesRepository.GetByID(id) is { } series ? (true, series.PreferredOrderingID) : (false, null);
        if (seriesID.Source == MetadataSource.AniDB)
            return seriesID.TryGetNumericID<int>(out var id) && anidbAnimeRepository.GetByAnimeID(id) is { } anime ? (true, anime.PreferredOrderingID) : (false, null);
        if (seriesID.Source == MetadataSource.TMDB)
            return seriesID.TryGetNumericID<int>(out var id) && tmdbShowRepository.GetByTmdbShowID(id) is { } show ? (true, show.PreferredOrderingID) : (false, null);
        if (seriesID.Source.IsCore)
            return (false, null);

        return seriesStore.Value.GetSeries(seriesID) is Metadata_Series stored ? (true, stored.PreferredOrderingID) : (false, null);
    }

    #endregion

    #region Episodes

    /// <inheritdoc />
    public bool HasEpisode(MetadataGuid episodeID)
        => Episode(episodeID).Found;

    /// <inheritdoc />
    public bool IsHidden(MetadataGuid episodeID)
        => Episode(episodeID).IsHidden;

    /// <inheritdoc />
    public bool SetHidden(MetadataGuid episodeID, bool hidden)
    {
        ArgumentNullException.ThrowIfNull(episodeID);
        if (episodeID.EntityType != MetadataEntityType.Episode)
            return false;

        if (episodeID.Source == MetadataSource.AniDB)
        {
            if (!episodeID.TryGetNumericID<int>(out var id) || anidbEpisodeRepository.GetByEpisodeID(id) is not { } episode || episode.IsHidden == hidden)
                return false;

            episode.IsHidden = hidden;
            anidbEpisodeRepository.Save(episode);
            return true;
        }

        if (episodeID.Source == MetadataSource.TMDB)
        {
            if (!episodeID.TryGetNumericID<int>(out var id) || tmdbEpisodeRepository.GetByTmdbEpisodeID(id) is not { } episode || episode.IsHidden == hidden)
                return false;

            episode.IsHidden = hidden;
            tmdbEpisodeRepository.Save(episode);
            return true;
        }

        return !episodeID.Source.IsCore && seriesStore.Value.SetEpisodeHidden(episodeID, hidden);
    }

    /// <summary>
    ///   Finds an episode's row and reads its hidden flag.
    /// </summary>
    /// <param name="episodeID">The episode.</param>
    /// <returns>Whether the row was found, and whether it is hidden.</returns>
    private (bool Found, bool IsHidden) Episode(MetadataGuid episodeID)
    {
        ArgumentNullException.ThrowIfNull(episodeID);
        if (episodeID.EntityType != MetadataEntityType.Episode)
            return (false, false);

        if (episodeID.Source == MetadataSource.AniDB)
            return episodeID.TryGetNumericID<int>(out var id) && anidbEpisodeRepository.GetByEpisodeID(id) is { } episode ? (true, episode.IsHidden) : (false, false);
        if (episodeID.Source == MetadataSource.TMDB)
            return episodeID.TryGetNumericID<int>(out var id) && tmdbEpisodeRepository.GetByTmdbEpisodeID(id) is { } episode ? (true, episode.IsHidden) : (false, false);
        if (episodeID.Source.IsCore)
            return (false, false);

        return seriesStore.Value.GetEpisode(episodeID) is Metadata_Episode stored ? (true, stored.IsHidden) : (false, false);
    }

    #endregion
}
