using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Trakt;
using Shoko.Server.Utilities;

#pragma warning disable ASP0023
#pragma warning disable CA2012
namespace Shoko.Server;

public partial class ShokoServiceImplementation : IShokoServer
{
    [HttpGet("AniDB/CrossRef/{animeID}")]
    public CL_AniDB_AnimeCrossRefs GetCrossRefDetails(int animeID)
    {
        var result = new CL_AniDB_AnimeCrossRefs
        {
            CrossRef_AniDB_TvDB = [],
            TvDBSeries = [],
            TvDBEpisodes = [],
            TvDBImageFanarts = [],
            TvDBImagePosters = [],
            TvDBImageWideBanners = [],
            CrossRef_AniDB_MovieDB = null,
            MovieDBMovie = null,
            MovieDBFanarts = [],
            MovieDBPosters = [],
            CrossRef_AniDB_MAL = null,
            CrossRef_AniDB_Trakt = [],
            TraktShows = [],
            AnimeID = animeID
        };

        try
        {
            using var session = Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().SessionFactory.OpenSession();
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime == null)
            {
                return result;
            }

            // TMDB
            var (xrefMovie, _) = anime.TmdbMovieCrossReferences;
            result.CrossRef_AniDB_MovieDB = xrefMovie?.ToClient();
            if (xrefMovie?.TmdbMovie is { } tmdbMovie)
            {
                result.MovieDBMovie = xrefMovie?.TmdbMovie?.ToClient();
                foreach (var fanart in tmdbMovie.GetImages(ImageEntityType.Backdrop))
                    result.MovieDBFanarts.Add(fanart.ToClientFanart());
                foreach (var poster in tmdbMovie.GetImages(ImageEntityType.Poster))
                    result.MovieDBPosters.Add(poster.ToClientPoster());
            }

            // MAL
            var xrefMAL = anime.MalCrossReferences;
            if (xrefMAL == null)
            {
                result.CrossRef_AniDB_MAL = null;
            }
            else
            {
                result.CrossRef_AniDB_MAL = [];
                foreach (var xrefTemp in xrefMAL)
                {
                    result.CrossRef_AniDB_MAL.Add(xrefTemp);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ex}", ex.ToString());
            return result;
        }
    }

    #region Web Cache Admin

    [HttpGet("WebCache/IsAdmin")]
    public bool IsWebCacheAdmin()
    {
        return false;
    }

    [HttpGet("WebCache/RandomLinkForApproval/{linkType}")]
    public object Admin_GetRandomLinkForApproval(int linkType)
    {
        return null;
    }

    [HttpGet("WebCache/AdminMessages")]
    public List<object> GetAdminMessages()
    {
        return [];
    }

    #region Admin - TvDB

    [HttpGet("WebCache/CrossRef/TvDB/{crossRef_AniDB_TvDBId}")]
    public string ApproveTVDBCrossRefWebCache(int crossRef_AniDB_TvDBId)
    {
        return "This feature has been disabled until further notice.";
    }

    [HttpDelete("WebCache/CrossRef/TvDB/{crossRef_AniDB_TvDBId}")]
    public string RevokeTVDBCrossRefWebCache(int crossRef_AniDB_TvDBId)
    {
        return "This feature has been disabled until further notice.";
    }

    /// <summary>
    /// Sends the current user's TvDB links to the web cache, and then admin approves them
    /// </summary>
    /// <returns></returns>
    [HttpPost("WebCache/TvDB/UseLinks/{animeID}")]
    public string UseMyTvDBLinksWebCache(int animeID)
    {
        return "This feature has been disabled until further notice.";
    }

    #endregion

    #region Admin - Trakt


    // The interface have these mapped to the same endpoint and same method, so just map them and let them conflict.
    [HttpPost("WebCache/CrossRef/Trakt/{crossRef_AniDB_TraktId}")]
    public string ApproveTraktCrossRefWebCache(int crossRef_AniDB_TraktId)
    {
        return "This feature has been disabled until further notice.";
    }

    [HttpPost("WebCache/CrossRef/Trakt/{crossRef_AniDB_TraktId}")]
    public string RevokeTraktCrossRefWebCache(int crossRef_AniDB_TraktId)
    {
        return "This feature has been disabled until further notice.";
    }

    /// <summary>
    /// Sends the current user's Trakt links to the web cache, and then admin approves them
    /// </summary>
    /// <returns></returns>
    [HttpPost("WebCache/Trakt/UseLinks/{animeID}")]
    public string UseMyTraktLinksWebCache(int animeID)
    {
        return "This feature has been disabled until further notice.";
    }

    #endregion

    #endregion

    #region TvDB

    [HttpPost("Series/TvDB/Refresh/{seriesID}")]
    public string UpdateTvDBData(int seriesID)
    {
        return string.Empty;
    }

    [HttpGet("TvDB/Language")]
    public List<object> GetTvDBLanguages()
    {
        return [];
    }

    [HttpGet("WebCache/CrossRef/TvDB/{animeID}/{isAdmin}")]
    public List<object> GetTVDBCrossRefWebCache(int animeID, bool isAdmin)
    {
        return [];
    }

    [HttpGet("TvDB/CrossRef/{animeID}")]
    public List<object> GetTVDBCrossRefV2(int animeID)
    {
        return [];
    }

    [HttpGet("TvDB/CrossRef/Preview/{animeID}/{tvdbID}")]
    public List<object> GetTvDBEpisodeMatchPreview(int animeID, int tvdbID)
    {
        return [];
    }

    [HttpGet("TvDB/CrossRef/Episode/{animeID}")]
    public List<object> GetTVDBCrossRefEpisode(int animeID)
    {
        return [];
    }

    [HttpGet("TvDB/Search/{criteria}")]
    public List<object> SearchTheTvDB(string criteria)
    {
        return [];
    }

    [HttpGet("Series/Seasons/{seriesID}")]
    public List<int> GetSeasonNumbersForSeries(int seriesID)
    {
        return [];
    }

    [HttpPost("TvDB/CrossRef")]
    public string LinkAniDBTvDB(object link)
    {
        return string.Empty;
    }

    [HttpPost("TvDB/CrossRef/FromWebCache")]
    public string LinkTvDBUsingWebCacheLinks(List<object> links)
    {
        return "The WebCache is disabled.";
    }

    [HttpPost("TvDB/CrossRef/Episode/{aniDBID}/{tvDBID}")]
    public string LinkAniDBTvDBEpisode(int aniDBID, int tvDBID)
    {
        return string.Empty;
    }

    /// <summary>
    /// Removes all tvdb links for one anime
    /// </summary>
    /// <param name="animeID"></param>
    /// <returns></returns>
    [HttpDelete("TvDB/CrossRef/{animeID}")]
    public string RemoveLinkAniDBTvDBForAnime(int animeID)
    {
        return string.Empty;
    }

    [HttpDelete("TvDB/CrossRef")]
    public string RemoveLinkAniDBTvDB(object link)
    {
        return string.Empty;
    }

    [HttpDelete("TvDB/CrossRef/Episode/{aniDBEpisodeID}/{tvDBEpisodeID}")]
    public string RemoveLinkAniDBTvDBEpisode(int aniDBEpisodeID, int tvDBEpisodeID)
    {
        return string.Empty;
    }

    [HttpGet("TvDB/Poster/{tvDBID?}")]
    public List<object> GetAllTvDBPosters(int? tvDBID)
    {
        return [];
    }

    [HttpGet("TvDB/Banner/{tvDBID?}")]
    public List<object> GetAllTvDBWideBanners(int? tvDBID)
    {
        return [];
    }

    [HttpGet("TvDB/Fanart/{tvDBID?}")]
    public List<object> GetAllTvDBFanart(int? tvDBID)
    {
        return [];
    }

    [HttpGet("TvDB/Episode/{tvDBID?}")]
    public List<object> GetAllTvDBEpisodes(int? tvDBID)
    {
        return [];
    }

    #endregion

    #region Trakt

    [HttpGet("Trakt/Episode/{traktShowID?}")]
    public List<Trakt_Episode> GetAllTraktEpisodes(int? traktShowID)
    {
        return [];
    }

    [HttpGet("Trakt/Episode/FromTraktId/{traktID}")]
    public List<Trakt_Episode> GetAllTraktEpisodesByTraktID(string traktID)
    {
        return [];
    }

    [HttpGet("WebCache/CrossRef/Trakt/{animeID}/{isAdmin}")]
    public List<object> GetTraktCrossRefWebCache(int animeID, bool isAdmin)
    {
        return [];
    }

    [HttpPost(
        "Trakt/CrossRef/{animeID}/{aniEpType}/{aniEpNumber}/{traktID}/{seasonNumber}/{traktEpNumber}/{crossRef_AniDB_TraktV2ID?}")]
    public string LinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID, int seasonNumber,
        int traktEpNumber, int? crossRef_AniDB_TraktV2ID)
    {
        return string.Empty;
    }

    [HttpGet("Trakt/CrossRef/{animeID}")]
    public List<CrossRef_AniDB_TraktV2> GetTraktCrossRefV2(int animeID)
    {
        return [];
    }

    [HttpGet("Trakt/CrossRef/Episode/{animeID}")]
    public List<CrossRef_AniDB_Trakt_Episode> GetTraktCrossRefEpisode(int animeID)
    {
        return [];
    }

    [HttpGet("Trakt/Search/{criteria}")]
    public List<CL_TraktTVShowResponse> SearchTrakt(string criteria)
    {
        return [];
    }

    [HttpDelete("Trakt/CrossRef/{animeID}")]
    public string RemoveLinkAniDBTraktForAnime(int animeID)
    {
        return string.Empty;
    }

    [HttpDelete("Trakt/CrossRef/{animeID}/{aniEpType}/{aniEpNumber}/{traktID}/{traktSeasonNumber}/{traktEpNumber}")]
    public string RemoveLinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID,
        int traktSeasonNumber,
        int traktEpNumber)
    {
        return string.Empty;
    }

    [HttpGet("Trakt/Seasons/{traktID}")]
    public List<int> GetSeasonNumbersForTrakt(string traktID)
    {
        return [];
    }

    [HttpDelete("Trakt/Friend/{friendUsername}")]
    public CL_Response<bool> TraktFriendRequestDeny(string friendUsername)
    {
        return new CL_Response<bool> { Result = false };
    }

    [HttpPost("Trakt/Friend/{friendUsername}")]
    public CL_Response<bool> TraktFriendRequestApprove(string friendUsername)
    {
        return new CL_Response<bool> { Result = false };
    }

    [HttpPost("Trakt/Scrobble/{animeId}/{type}/{progress}/{status}")]
    public int TraktScrobble(int animeId, int type, int progress, int status)
    {
        try
        {
            var statusTraktV2 = ScrobblePlayingStatus.Start;

            switch (status)
            {
                case (int)ScrobblePlayingStatus.Start:
                    statusTraktV2 = ScrobblePlayingStatus.Start;
                    break;
                case (int)ScrobblePlayingStatus.Pause:
                    statusTraktV2 = ScrobblePlayingStatus.Pause;
                    break;
                case (int)ScrobblePlayingStatus.Stop:
                    statusTraktV2 = ScrobblePlayingStatus.Stop;
                    break;
            }

            var isValidProgress = float.TryParse(progress.ToString(), out var progressTrakt);

            if (isValidProgress)
            {
                var animeEpisode = RepoFactory.AnimeEpisode.GetByID(animeId);
                return _traktHelper.Scrobble(animeEpisode, statusTraktV2, progressTrakt);
            }

            return 500;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ex}", ex.ToString());
            return 500;
        }
    }

    [HttpPost("Trakt/Refresh/{traktID}")]
    public string UpdateTraktData(string traktID)
    {
        return string.Empty;
    }

    [HttpPost("Trakt/Sync/{animeID}")]
    public string SyncTraktSeries(int animeID)
    {
        try
        {
            if (!_settingsProvider.GetSettings().TraktTv.Enabled)
            {
                return string.Empty;
            }

            var ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
            if (ser == null)
            {
                return "Could not find Anime Series";
            }

            var scheduler = _schedulerFactory.GetScheduler().Result;
            scheduler.StartJob<SyncSeriesWatchStatusToTraktJob>(c => c.AnimeSeriesID = ser.AnimeSeriesID).GetAwaiter().GetResult();

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ex}", ex.ToString());
            return ex.Message;
        }
    }

    [HttpPost("Trakt/Comment/{traktID}/{isSpoiler}")]
    public CL_Response<bool> PostTraktCommentShow(string traktID, string commentText, bool isSpoiler)
    {
        return new CL_Response<bool>() { Result = false };
    }

    [HttpPost("Trakt/LinkValidity/{slug}/{removeDBEntries}")]
    public bool CheckTraktLinkValidity(string slug, bool removeDBEntries)
    {
        return true;
    }

    [HttpGet("Trakt/CrossRef")]
    public List<CrossRef_AniDB_TraktV2> GetAllTraktCrossRefs()
    {
        return [];
    }

    [HttpGet("Trakt/Comment/{animeID}")]
    public List<CL_Trakt_CommentUser> GetTraktCommentsForAnime(int animeID)
    {
        return [];
    }

    [HttpGet("Trakt/DeviceCode")]
    public CL_TraktDeviceCode GetTraktDeviceCode()
    {
        try
        {
            var response = _traktHelper.GetTraktDeviceCode();
            return new CL_TraktDeviceCode { VerificationUrl = response.VerificationUrl, UserCode = response.UserCode };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetTraktDeviceCode: {ex}", ex.ToString());
            return null;
        }
    }

    #endregion

    #region Other Cross Refs

    [HttpGet("WebCache/CrossRef/Other/{animeID}/{crossRefType}")]
    public object GetOtherAnimeCrossRefWebCache(int animeID, int crossRefType)
    {
        return null;
    }

    [HttpGet("Other/CrossRef/{animeID}/{crossRefType}")]
    public CrossRef_AniDB_Other GetOtherAnimeCrossRef(int animeID, int crossRefType)
    {
        try
        {
            if (crossRefType != 1 /* CrossRefType.MovieDB */)
                return null;
            var (xref, _) = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(animeID);
            return xref?.ToClient();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ex}", ex.ToString());
            return null;
        }
    }

    [HttpPost("Other/CrossRef/{animeID}/{id}/{crossRefType}")]
    public string LinkAniDBOther(int animeID, int id, int crossRefType)
    {
        try
        {
            switch (crossRefType)
            {
                case 1 /* CrossRefType.MovieDB */:
                    var episodeId = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeTypeNumber(animeID, EpisodeType.Episode, 1).FirstOrDefault()?.EpisodeID;
                    if (!episodeId.HasValue || episodeId <= 0)
                        return $"Could not find first episode for AniDB Anime {animeID} to link to for TMDB Movie {id}";
                    _tmdbLinkingService.AddMovieLinkForEpisode(episodeId.Value, id).ConfigureAwait(false).GetAwaiter().GetResult();
                    _tmdbMetadataService.ScheduleUpdateOfMovie(id, downloadImages: true).ConfigureAwait(false).GetAwaiter().GetResult();
                    break;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ex}", ex.ToString());
            return ex.Message;
        }
    }

    [HttpDelete("Other/CrossRef/{animeID}/{crossRefType}")]
    public string RemoveLinkAniDBOther(int animeID, int crossRefType)
    {
        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);

            if (anime == null)
            {
                return "Could not find Anime!";
            }

            switch (crossRefType)
            {
                case 1 /* CrossRefType.MovieDB */:
                    _tmdbLinkingService.RemoveAllMovieLinksForAnime(animeID).ConfigureAwait(false).GetAwaiter().GetResult();
                    break;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ex}", ex.ToString());
            return ex.Message;
        }
    }

    #endregion

    #region MovieDB

    [HttpGet("MovieDB/Search/{criteria}")]
    public List<CL_MovieDBMovieSearch_Response> SearchTheMovieDB(string criteria)
    {
        var results = new List<CL_MovieDBMovieSearch_Response>();
        try
        {
            var (movieResults, _) = _tmdbSearchService.SearchMovies(System.Web.HttpUtility.UrlDecode(criteria)).ConfigureAwait(false).GetAwaiter().GetResult();

            results.AddRange(movieResults.Select(movie => movie.ToContract()));

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ex}", ex.ToString());
            return results;
        }
    }

    [HttpGet("MovieDB/Poster/{movieID?}")]
    public List<CL_MovieDB_Poster> GetAllMovieDBPosters(int? movieID)
    {
        try
        {
            if (movieID.HasValue)
                return RepoFactory.TMDB_Image.GetByTmdbMovieIDAndType(movieID.Value, ImageEntityType.Poster)
                    .Select(image => image.ToClientPoster())
                    .ToList();

            return RepoFactory.TMDB_Image.GetByType(ImageEntityType.Poster)
                .Select(image => image.ToClientPoster())
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ex}", ex.ToString());
            return [];
        }
    }

    [HttpGet("MovieDB/Fanart/{movieID?}")]
    public List<CL_MovieDB_Fanart> GetAllMovieDBFanart(int? movieID)
    {
        try
        {
            if (movieID.HasValue)
                return RepoFactory.TMDB_Image.GetByTmdbMovieIDAndType(movieID.Value, ImageEntityType.Backdrop)
                    .Select(image => image.ToClientFanart())
                    .ToList();

            return RepoFactory.TMDB_Image.GetByType(ImageEntityType.Backdrop)
                .Select(image => image.ToClientFanart())
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ex}", ex.ToString());
            return [];
        }
    }

    [HttpPost("MovieDB/Refresh/{movieID}")]
    public string UpdateMovieDBData(int movieID)
    {
        try
        {
            _tmdbMetadataService.ScheduleUpdateOfMovie(movieID, downloadImages: true, forceRefresh: true).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ex}", ex.ToString());
        }

        return string.Empty;
    }

    #endregion
}
