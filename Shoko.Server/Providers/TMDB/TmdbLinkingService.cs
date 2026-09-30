using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.Services;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.CrossReference.Embedded;
using Shoko.Server.Repositories.Cached.AniDB;

namespace Shoko.Server.Providers.TMDB;

/// <summary>
///   TMDB's own linking service, kept for the callers that name TMDB, over
///   the generic linking service.
/// </summary>
/// <remarks>
///   It holds no TMDB logic of its own. Each call is passed on to
///   <see cref="IMetadataLinkingService"/> for the <c>tmdb</c> source, which
///   writes the links and asks the TMDB provider to match the episodes.
///   Removing a link by hand tells TMDB to leave the anime alone, as it
///   always did. Linking queues no refresh: the caller decides, as TMDB's
///   own endpoints do.
/// </remarks>
/// <param name="linkingService">Writes and removes the links.</param>
/// <param name="metadataService">Tells whether a TMDB episode is stored.</param>
/// <param name="anidbEpisodes">The AniDB episodes, for the anime an episode belongs to.</param>
public class TmdbLinkingService(
    IMetadataLinkingService linkingService,
    IMetadataService metadataService,
    AniDB_EpisodeRepository anidbEpisodes
) : ITmdbLinkingService
{
    #region Shared

    /// <inheritdoc />
    public void RemoveAllLinks(bool removeShowLinks = true, bool removeMovieLinks = true)
        => linkingService.RemoveAllLinks(MetadataSource.TMDB, removeShowLinks, removeMovieLinks).GetAwaiter().GetResult();

    /// <inheritdoc />
    public void ResetAutoLinkingState(bool disabled = false)
        => linkingService.ResetAutoLinkingState(MetadataSource.TMDB, disabled);

    #endregion

    #region Movie Links

    /// <inheritdoc />
    public async Task AddMovieLinkForEpisode(int anidbEpisodeId, int tmdbMovieId, bool additiveLink = false, MatchRating matchRating = MatchRating.UserVerified)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tmdbMovieId);
        if (anidbEpisodes.GetByEpisodeID(anidbEpisodeId) is not { } episode)
            return;

        await linkingService.AddMovieLink(new()
        {
            Source = MetadataSource.TMDB,
            EntityType = MetadataEntityType.Movie,
            ProviderID = MovieID(tmdbMovieId),
            AnidbEpisodeID = anidbEpisodeId,
            AnidbAnimeID = episode.AnimeID,
            Additive = additiveLink,
            MatchRating = matchRating,
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveMovieLinkForEpisode(int anidbEpisodeId, int tmdbMovieId, bool purge = false)
        => await linkingService.RemoveMovieLink(new()
        {
            Source = MetadataSource.TMDB,
            EntityType = MetadataEntityType.Movie,
            ProviderID = MovieID(tmdbMovieId),
            AnidbEpisodeID = anidbEpisodeId,
            AnidbAnimeID = anidbEpisodes.GetByEpisodeID(anidbEpisodeId)?.AnimeID ?? 0,
            Purge = purge,
            DisableAutoLinking = true,
        }).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task RemoveAllMovieLinksForAnime(int anidbAnimeId, bool purge = false)
        => await linkingService.RemoveLinksForAnime(MetadataSource.TMDB, anidbAnimeId, MetadataEntityType.Movie, purge, disableAutoLinking: true)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task RemoveAllMovieLinksForEpisode(int anidbEpisodeId, bool purge = false)
        => await linkingService.RemoveLinksForEpisode(MetadataSource.TMDB, anidbEpisodeId, MetadataEntityType.Movie, purge, disableAutoLinking: true)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task RemoveAllMovieLinksForMovie(int tmdbMovieId)
        => await linkingService.RemoveLinksTo(MovieID(tmdbMovieId)).ConfigureAwait(false);

    #endregion

    #region Show Links

    /// <inheritdoc />
    public async Task AddShowLink(int anidbAnimeId, int tmdbShowId, bool additiveLink = true, MatchRating matchRating = MatchRating.UserVerified)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tmdbShowId);
        var showID = ShowID(tmdbShowId);
        await linkingService.AddSeriesLink(new()
        {
            Source = MetadataSource.TMDB,
            EntityType = MetadataEntityType.Series,
            ProviderID = showID,
            AnidbAnimeID = anidbAnimeId,
            Additive = additiveLink,
            MatchRating = matchRating,
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveShowLink(int anidbAnimeId, int tmdbShowId, bool purge = false)
        => await linkingService.RemoveSeriesLink(new()
        {
            Source = MetadataSource.TMDB,
            EntityType = MetadataEntityType.Series,
            ProviderID = ShowID(tmdbShowId),
            AnidbAnimeID = anidbAnimeId,
            Purge = purge,
            DisableAutoLinking = true,
        }).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task RemoveAllShowLinksForAnime(int animeId, bool purge = false)
        => await linkingService.RemoveLinksForAnime(MetadataSource.TMDB, animeId, MetadataEntityType.Series, purge, disableAutoLinking: true)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task RemoveAllShowLinksForShow(int showId)
        => await linkingService.RemoveLinksTo(ShowID(showId)).ConfigureAwait(false);

    #endregion

    #region Episode Links

    /// <inheritdoc />
    public void ResetAllEpisodeLinks(int anidbAnimeId, bool allowAuto)
        => linkingService.ResetEpisodeLinks(MetadataSource.TMDB, anidbAnimeId, allowAuto).GetAwaiter().GetResult();

    /// <inheritdoc />
    public bool SetEpisodeLink(int anidbEpisodeId, int tmdbEpisodeId, bool additiveLink = true, int? index = null)
    {
        // A zero is an explicitly empty link, which always replaces the rest.
        if (tmdbEpisodeId is 0)
            return linkingService.SetEpisodeLink(MetadataSource.TMDB, anidbEpisodeId, null, additive: false).GetAwaiter().GetResult();

        var episodeID = EpisodeID(tmdbEpisodeId);
        if (metadataService.GetEpisode(episodeID) is null)
            return false;

        return linkingService.SetEpisodeLink(MetadataSource.TMDB, anidbEpisodeId, episodeID, additiveLink, index).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public IReadOnlyList<IMetadataEpisodeCrossReference<ITmdbEpisode>> MatchAnidbToTmdbEpisodes(
        int anidbAnimeId,
        int tmdbShowId,
        int? tmdbSeasonId,
        bool useExisting = false,
        bool saveToDatabase = false,
        bool? useExistingOtherShows = null
    )
    {
        var seasonID = tmdbSeasonId is { } seasonId ? new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Season, seasonId.ToString()) : null;
        var links = linkingService.MatchEpisodes(anidbAnimeId, ShowID(tmdbShowId), seasonID, useExisting, saveToDatabase, useExistingOtherShows)
            .GetAwaiter()
            .GetResult();
        return [.. links.OfType<CrossRef_AniDB_Metadata_Episode>().Select(row => new CrossRef_AniDB_TMDB_Episode(row))];
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   The identifier of a TMDB show.
    /// </summary>
    /// <param name="showId">The TMDB show ID.</param>
    /// <returns>The identifier.</returns>
    private static MetadataGuid ShowID(int showId)
        => new(MetadataSource.TMDB, MetadataEntityType.Series, showId.ToString());

    /// <summary>
    ///   The identifier of a TMDB movie.
    /// </summary>
    /// <param name="movieId">The TMDB movie ID.</param>
    /// <returns>The identifier.</returns>
    private static MetadataGuid MovieID(int movieId)
        => new(MetadataSource.TMDB, MetadataEntityType.Movie, movieId.ToString());

    /// <summary>
    ///   The identifier of a TMDB episode.
    /// </summary>
    /// <param name="episodeId">The TMDB episode ID.</param>
    /// <returns>The identifier.</returns>
    private static MetadataGuid EpisodeID(int episodeId)
        => new(MetadataSource.TMDB, MetadataEntityType.Episode, episodeId.ToString());

    #endregion
}
