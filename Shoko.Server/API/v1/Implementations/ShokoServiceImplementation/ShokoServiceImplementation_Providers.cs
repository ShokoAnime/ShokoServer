﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Models.Azure;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Models.Server;
using Shoko.Models.TvDB;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Trakt;
using Shoko.Server.Scheduling.Jobs.TvDB;
using Shoko.Server.Utilities;

namespace Shoko.Server;

public partial class ShokoServiceImplementation : IShokoServer
{
    [HttpGet("AniDB/CrossRef/{animeID}")]
    public CL_AniDB_AnimeCrossRefs GetCrossRefDetails(int animeID)
    {
        var result = new CL_AniDB_AnimeCrossRefs
        {
            CrossRef_AniDB_TvDB = new List<CrossRef_AniDB_TvDBV2>(),
            TvDBSeries = new List<TvDB_Series>(),
            TvDBEpisodes = new List<TvDB_Episode>(),
            TvDBImageFanarts = new List<TvDB_ImageFanart>(),
            TvDBImagePosters = new List<TvDB_ImagePoster>(),
            TvDBImageWideBanners = new List<TvDB_ImageWideBanner>(),
            CrossRef_AniDB_MovieDB = null,
            MovieDBMovie = null,
            MovieDBFanarts = new List<MovieDB_Fanart>(),
            MovieDBPosters = new List<MovieDB_Poster>(),
            CrossRef_AniDB_MAL = null,
            CrossRef_AniDB_Trakt = new List<CrossRef_AniDB_TraktV2>(),
            TraktShows = new List<CL_Trakt_Show>(),
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

            var xrefs = RepoFactory.CrossRef_AniDB_TvDB.GetV2LinksFromAnime(animeID);

            // TvDB
            result.CrossRef_AniDB_TvDB = xrefs;

            foreach (var ep in anime.TvDBEpisodes)
            {
                result.TvDBEpisodes.Add(ep);
            }

            foreach (var xref in xrefs.DistinctBy(a => a.TvDBID))
            {
                var ser = RepoFactory.TvDB_Series.GetByTvDBID(xref.TvDBID);
                if (ser != null)
                {
                    result.TvDBSeries.Add(ser);
                }

                foreach (var fanart in RepoFactory.TvDB_ImageFanart.GetBySeriesID(xref.TvDBID))
                {
                    result.TvDBImageFanarts.Add(fanart);
                }

                foreach (var poster in RepoFactory.TvDB_ImagePoster.GetBySeriesID(xref.TvDBID))
                {
                    result.TvDBImagePosters.Add(poster);
                }

                foreach (var banner in RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(xref
                             .TvDBID))
                {
                    result.TvDBImageWideBanners.Add(banner);
                }
            }

            // Trakt


            foreach (var xref in anime.GetCrossRefTraktV2())
            {
                result.CrossRef_AniDB_Trakt.Add(xref);

                var show = RepoFactory.Trakt_Show.GetByTraktSlug(session, xref.TraktID);
                if (show != null)
                {
                    result.TraktShows.Add(show.ToClient());
                }
            }


            // MovieDB
            var xrefMovie = anime.CrossRefMovieDB;
            result.CrossRef_AniDB_MovieDB = xrefMovie;


            result.MovieDBMovie = anime.MovieDBMovie;


            foreach (var fanart in anime.MovieDBFanarts)
            {
                if (fanart.ImageSize.Equals(Shoko.Models.Constants.MovieDBImageSize.Original,
                        StringComparison.InvariantCultureIgnoreCase))
                {
                    result.MovieDBFanarts.Add(fanart);
                }
            }

            foreach (var poster in anime.MovieDBPosters)
            {
                if (poster.ImageSize.Equals(Shoko.Models.Constants.MovieDBImageSize.Original,
                        StringComparison.InvariantCultureIgnoreCase))
                {
                    result.MovieDBPosters.Add(poster);
                }
            }

            // MAL
            var xrefMAL = anime.GetCrossRefMAL();
            if (xrefMAL == null)
            {
                result.CrossRef_AniDB_MAL = null;
            }
            else
            {
                result.CrossRef_AniDB_MAL = new List<CrossRef_AniDB_MAL>();
                foreach (var xrefTemp in xrefMAL)
                {
                    result.CrossRef_AniDB_MAL.Add(xrefTemp);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
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
    public Azure_AnimeLink Admin_GetRandomLinkForApproval(int linkType)
    {
        return null;
    }

    [HttpGet("WebCache/AdminMessages")]
    public List<Azure_AdminMessage> GetAdminMessages()
    {
        return new List<Azure_AdminMessage>();
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
        try
        {
            _schedulerFactory.GetScheduler().Result.StartJobNow<GetTvDBSeriesJob>(
                c =>
                {
                    c.TvDBSeriesID = seriesID;
                    c.ForceRefresh = true;
                }
            ).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
        }

        return string.Empty;
    }

    [HttpGet("TvDB/Language")]
    public List<TvDB_Language> GetTvDBLanguages()
    {
        try
        {
            return _tvdbHelper.GetLanguagesAsync().Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
        }

        return new List<TvDB_Language>();
    }

    [HttpGet("WebCache/CrossRef/TvDB/{animeID}/{isAdmin}")]
    public List<Azure_CrossRef_AniDB_TvDB> GetTVDBCrossRefWebCache(int animeID, bool isAdmin)
    {
        try
        {
            return new List<Azure_CrossRef_AniDB_TvDB>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new List<Azure_CrossRef_AniDB_TvDB>();
        }
    }

    [HttpGet("TvDB/CrossRef/{animeID}")]
    public List<CrossRef_AniDB_TvDBV2> GetTVDBCrossRefV2(int animeID)
    {
        try
        {
            return RepoFactory.CrossRef_AniDB_TvDB.GetV2LinksFromAnime(animeID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new List<CrossRef_AniDB_TvDBV2>();
        }
    }

    [HttpGet("TvDB/CrossRef/Preview/{animeID}/{tvdbID}")]
    public List<CrossRef_AniDB_TvDB_Episode> GetTvDBEpisodeMatchPreview(int animeID, int tvdbID)
    {
        return TvDBLinkingHelper.GetMatchPreviewWithOverrides(animeID, tvdbID);
    }

    [HttpGet("TvDB/CrossRef/Episode/{animeID}")]
    public List<CrossRef_AniDB_TvDB_Episode_Override> GetTVDBCrossRefEpisode(int animeID)
    {
        try
        {
            return RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAnimeID(animeID).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new List<CrossRef_AniDB_TvDB_Episode_Override>();
        }
    }

    [HttpGet("TvDB/Search/{criteria}")]
    public List<TVDB_Series_Search_Response> SearchTheTvDB(string criteria)
    {
        try
        {
            return _tvdbHelper.SearchSeriesAsync(criteria).Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new List<TVDB_Series_Search_Response>();
        }
    }

    [HttpGet("Series/Seasons/{seriesID}")]
    public List<int> GetSeasonNumbersForSeries(int seriesID)
    {
        var seasonNumbers = new List<int>();
        try
        {
            // refresh data from TvDB
            _tvdbHelper.UpdateSeriesInfoAndImages(seriesID, true, false).GetAwaiter().GetResult();

            seasonNumbers = RepoFactory.TvDB_Episode.GetSeasonNumbersForSeries(seriesID);

            return seasonNumbers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return seasonNumbers;
        }
    }

    [HttpPost("TvDB/CrossRef")]
    public string LinkAniDBTvDB(CrossRef_AniDB_TvDBV2 link)
    {
        try
        {
            var xref = RepoFactory.CrossRef_AniDB_TvDB.GetByAniDBAndTvDBID(link.AnimeID, link.TvDBID);

            if (xref != null && link.IsAdditive)
            {
                var msg = $"You have already linked Anime ID {xref.AniDBID} to this TvDB show/season/ep";
                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref.AniDBID);
                if (anime != null)
                {
                    msg =
                        $"You have already linked Anime {anime.MainTitle} ({xref.AniDBID}) to this TvDB show/season/ep";
                }

                return msg;
            }

            // we don't need to proactively remove the link here anymore, as all links are removed when it is not marked as additive
            _schedulerFactory.GetScheduler().Result.StartJobNow<LinkTvDBSeriesJob>(c =>
                {
                    c.AnimeID = link.AnimeID;
                    c.TvDBID = link.TvDBID;
                    c.AdditiveLink = link.IsAdditive;
                }
            ).GetAwaiter().GetResult();

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpPost("TvDB/CrossRef/FromWebCache")]
    public string LinkTvDBUsingWebCacheLinks(List<CrossRef_AniDB_TvDBV2> links)
    {
        return "The WebCache is disabled.";
    }

    [HttpPost("TvDB/CrossRef/Episode/{aniDBID}/{tvDBID}")]
    public string LinkAniDBTvDBEpisode(int aniDBID, int tvDBID)
    {
        try
        {
            _tvdbHelper.LinkAniDBTvDBEpisode(aniDBID, tvDBID);

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return ex.Message;
        }
    }

    /// <summary>
    /// Removes all tvdb links for one anime
    /// </summary>
    /// <param name="animeID"></param>
    /// <returns></returns>
    [HttpDelete("TvDB/CrossRef/{animeID}")]
    public string RemoveLinkAniDBTvDBForAnime(int animeID)
    {
        try
        {
            var ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

            if (ser == null)
            {
                return "Could not find Series for Anime!";
            }

            var xrefs = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(animeID);
            if (xrefs == null)
            {
                return string.Empty;
            }

            foreach (var xref in xrefs)
            {
                // check if there are default images used associated
                var images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
                foreach (var image in images)
                {
                    if (image.ImageParentType == (int)ImageEntityType.TvDB_Banner ||
                        image.ImageParentType == (int)ImageEntityType.TvDB_Cover ||
                        image.ImageParentType == (int)ImageEntityType.TvDB_FanArt)
                    {
                        if (image.ImageParentID == xref.TvDBID)
                        {
                            RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                        }
                    }
                }

                _tvdbHelper.RemoveLinkAniDBTvDB(xref.AniDBID, xref.TvDBID);
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpDelete("TvDB/CrossRef")]
    public string RemoveLinkAniDBTvDB(CrossRef_AniDB_TvDBV2 link)
    {
        try
        {
            var ser = RepoFactory.AnimeSeries.GetByAnimeID(link.AnimeID);

            if (ser == null)
            {
                return "Could not find Series for Anime!";
            }

            // check if there are default images used associated
            var images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(link.AnimeID);
            foreach (var image in images)
            {
                if (image.ImageParentType == (int)ImageEntityType.TvDB_Banner ||
                    image.ImageParentType == (int)ImageEntityType.TvDB_Cover ||
                    image.ImageParentType == (int)ImageEntityType.TvDB_FanArt)
                {
                    if (image.ImageParentID == link.TvDBID)
                    {
                        RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                    }
                }
            }

            _tvdbHelper.RemoveLinkAniDBTvDB(link.AnimeID, link.TvDBID);

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpDelete("TvDB/CrossRef/Episode/{aniDBEpisodeID}/{tvDBEpisodeID}")]
    public string RemoveLinkAniDBTvDBEpisode(int aniDBEpisodeID, int tvDBEpisodeID)
    {
        try
        {
            var ep = RepoFactory.AniDB_Episode.GetByEpisodeID(aniDBEpisodeID);

            if (ep == null)
            {
                return "Could not find Episode";
            }

            var xref =
                RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAniDBAndTvDBEpisodeIDs(aniDBEpisodeID,
                    tvDBEpisodeID);
            if (xref == null)
            {
                return "Could not find Link!";
            }


            RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.Delete(xref.CrossRef_AniDB_TvDB_Episode_OverrideID);

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpGet("TvDB/Poster/{tvDBID?}")]
    public List<TvDB_ImagePoster> GetAllTvDBPosters(int? tvDBID)
    {
        var allImages = new List<TvDB_ImagePoster>();
        try
        {
            if (tvDBID.HasValue)
            {
                return RepoFactory.TvDB_ImagePoster.GetBySeriesID(tvDBID.Value);
            }

            return RepoFactory.TvDB_ImagePoster.GetAll().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new List<TvDB_ImagePoster>();
        }
    }

    [HttpGet("TvDB/Banner/{tvDBID?}")]
    public List<TvDB_ImageWideBanner> GetAllTvDBWideBanners(int? tvDBID)
    {
        try
        {
            if (tvDBID.HasValue)
            {
                return RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(tvDBID.Value);
            }

            return RepoFactory.TvDB_ImageWideBanner.GetAll().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new List<TvDB_ImageWideBanner>();
        }
    }

    [HttpGet("TvDB/Fanart/{tvDBID?}")]
    public List<TvDB_ImageFanart> GetAllTvDBFanart(int? tvDBID)
    {
        try
        {
            if (tvDBID.HasValue)
            {
                return RepoFactory.TvDB_ImageFanart.GetBySeriesID(tvDBID.Value);
            }

            return RepoFactory.TvDB_ImageFanart.GetAll().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new List<TvDB_ImageFanart>();
        }
    }

    [HttpGet("TvDB/Episode/{tvDBID?}")]
    public List<TvDB_Episode> GetAllTvDBEpisodes(int? tvDBID)
    {
        try
        {
            if (tvDBID.HasValue)
            {
                return RepoFactory.TvDB_Episode.GetBySeriesID(tvDBID.Value);
            }

            return RepoFactory.TvDB_Episode.GetAll().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new List<TvDB_Episode>();
        }
    }

    #endregion

    #region Trakt

    [HttpGet("Trakt/Episode/{traktShowID?}")]
    public List<Trakt_Episode> GetAllTraktEpisodes(int? traktShowID)
    {
        try
        {
            if (traktShowID.HasValue)
            {
                return RepoFactory.Trakt_Episode.GetByShowID(traktShowID.Value).ToList();
            }

            return RepoFactory.Trakt_Episode.GetAll().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new List<Trakt_Episode>();
        }
    }

    [HttpGet("Trakt/Episode/FromTraktId/{traktID}")]
    public List<Trakt_Episode> GetAllTraktEpisodesByTraktID(string traktID)
    {
        try
        {
            var show = RepoFactory.Trakt_Show.GetByTraktSlug(traktID);
            if (show != null)
            {
                return GetAllTraktEpisodes(show.Trakt_ShowID);
            }

            return new List<Trakt_Episode>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new List<Trakt_Episode>();
        }
    }

    [HttpGet("WebCache/CrossRef/Trakt/{animeID}/{isAdmin}")]
    public List<Azure_CrossRef_AniDB_Trakt> GetTraktCrossRefWebCache(int animeID, bool isAdmin)
    {
        try
        {
            return new List<Azure_CrossRef_AniDB_Trakt>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new List<Azure_CrossRef_AniDB_Trakt>();
        }
    }

    [HttpPost(
        "Trakt/CrossRef/{animeID}/{aniEpType}/{aniEpNumber}/{traktID}/{seasonNumber}/{traktEpNumber}/{crossRef_AniDB_TraktV2ID?}")]
    public string LinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID, int seasonNumber,
        int traktEpNumber, int? crossRef_AniDB_TraktV2ID)
    {
        try
        {
            if (crossRef_AniDB_TraktV2ID.HasValue)
            {
                var xrefTemp =
                    RepoFactory.CrossRef_AniDB_TraktV2.GetByID(crossRef_AniDB_TraktV2ID.Value);
                // delete the existing one if we are updating
                _traktHelper.RemoveLinkAniDBTrakt(xrefTemp.AnimeID, (EpisodeType)xrefTemp.AniDBStartEpisodeType,
                    xrefTemp.AniDBStartEpisodeNumber,
                    xrefTemp.TraktID, xrefTemp.TraktSeasonNumber, xrefTemp.TraktStartEpisodeNumber);
            }

            var xref = RepoFactory.CrossRef_AniDB_TraktV2.GetByTraktID(traktID, seasonNumber,
                traktEpNumber, animeID,
                aniEpType,
                aniEpNumber);
            if (xref != null)
            {
                var msg = string.Format("You have already linked Anime ID {0} to this Trakt show/season/ep",
                    xref.AnimeID);
                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref.AnimeID);
                if (anime != null)
                {
                    msg = string.Format("You have already linked Anime {0} ({1}) to this Trakt show/season/ep",
                        anime.MainTitle,
                        xref.AnimeID);
                }

                return msg;
            }

            return _traktHelper.LinkAniDBTrakt(animeID, (EpisodeType)aniEpType, aniEpNumber, traktID,
                seasonNumber,
                traktEpNumber, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpGet("Trakt/CrossRef/{animeID}")]
    public List<CrossRef_AniDB_TraktV2> GetTraktCrossRefV2(int animeID)
    {
        try
        {
            return RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(animeID).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new List<CrossRef_AniDB_TraktV2>();
        }
    }

    [HttpGet("Trakt/CrossRef/Episode/{animeID}")]
    public List<CrossRef_AniDB_Trakt_Episode> GetTraktCrossRefEpisode(int animeID)
    {
        return new List<CrossRef_AniDB_Trakt_Episode>();
    }

    [HttpGet("Trakt/Search/{criteria}")]
    public List<CL_TraktTVShowResponse> SearchTrakt(string criteria)
    {
        var results = new List<CL_TraktTVShowResponse>();
        try
        {
            var traktResults = _traktHelper.SearchShowV2(criteria);

            foreach (var res in traktResults)
            {
                results.Add(res.ToContract());
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return results;
        }
    }

    [HttpDelete("Trakt/CrossRef/{animeID}")]
    public string RemoveLinkAniDBTraktForAnime(int animeID)
    {
        try
        {
            var ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

            if (ser == null)
            {
                return "Could not find Series for Anime!";
            }

            // check if there are default images used associated
            var images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
            foreach (var image in images)
            {
                if (image.ImageParentType == (int)ImageEntityType.Trakt_Fanart ||
                    image.ImageParentType == (int)ImageEntityType.Trakt_Poster)
                {
                    RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                }
            }

            foreach (var xref in RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(animeID))
            {
                _traktHelper.RemoveLinkAniDBTrakt(animeID, (EpisodeType)xref.AniDBStartEpisodeType,
                    xref.AniDBStartEpisodeNumber,
                    xref.TraktID, xref.TraktSeasonNumber, xref.TraktStartEpisodeNumber);
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpDelete("Trakt/CrossRef/{animeID}/{aniEpType}/{aniEpNumber}/{traktID}/{traktSeasonNumber}/{traktEpNumber}")]
    public string RemoveLinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID,
        int traktSeasonNumber,
        int traktEpNumber)
    {
        try
        {
            var ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

            if (ser == null)
            {
                return "Could not find Series for Anime!";
            }

            // check if there are default images used associated
            var images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
            foreach (var image in images)
            {
                if (image.ImageParentType == (int)ImageEntityType.Trakt_Fanart ||
                    image.ImageParentType == (int)ImageEntityType.Trakt_Poster)
                {
                    RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                }
            }

            _traktHelper.RemoveLinkAniDBTrakt(animeID, (EpisodeType)aniEpType, aniEpNumber,
                traktID, traktSeasonNumber, traktEpNumber);

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpGet("Trakt/Seasons/{traktID}")]
    public List<int> GetSeasonNumbersForTrakt(string traktID)
    {
        var seasonNumbers = new List<int>();
        try
        {
            // refresh show info including season numbers from trakt
            _traktHelper.GetShowInfoV2(traktID);

            var show = RepoFactory.Trakt_Show.GetByTraktSlug(traktID);
            if (show == null)
            {
                return seasonNumbers;
            }

            foreach (var season in show.GetTraktSeasons())
            {
                seasonNumbers.Add(season.Season);
            }

            return seasonNumbers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return seasonNumbers;
        }
    }

    [HttpDelete("Trakt/Friend/{friendUsername}")]
    public CL_Response<bool> TraktFriendRequestDeny(string friendUsername)
    {
        return new CL_Response<bool> { Result = false };
        /*
        try
        {
            return TraktTVHelper.FriendRequestDeny(friendUsername, ref returnMessage);
        }
        catch (Exception ex)
        {
            logger.LogError( ex,"Error in TraktFriendRequestDeny: " + ex.ToString());
            returnMessage = ex.Message;
            return false;
        }*/
    }

    [HttpPost("Trakt/Friend/{friendUsername}")]
    public CL_Response<bool> TraktFriendRequestApprove(string friendUsername)
    {
        return new CL_Response<bool> { Result = false };
        /*
        try
        {
            return TraktTVHelper.FriendRequestApprove(friendUsername, ref returnMessage);
        }
        catch (Exception ex)
        {
            logger.LogError( ex,"Error in TraktFriendRequestDeny: " + ex.ToString());
            returnMessage = ex.Message;
            return false;
        }*/
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
                switch (type)
                {
                    // Movie
                    case (int)ScrobblePlayingType.movie:
                        return _traktHelper.Scrobble(
                            ScrobblePlayingType.movie, animeId.ToString(),
                            statusTraktV2, progressTrakt);
                    // TV episode
                    case (int)ScrobblePlayingType.episode:
                        return _traktHelper.Scrobble(
                            ScrobblePlayingType.episode,
                            animeId.ToString(), statusTraktV2, progressTrakt);
                }
            }

            return 500;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return 500;
        }
    }

    [HttpPost("Trakt/Refresh/{traktID}")]
    public string UpdateTraktData(string traktID)
    {
        try
        {
            _traktHelper.UpdateAllInfo(traktID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
        }

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
            scheduler.StartJob<SyncTraktCollectionSeriesJob>(c => c.AnimeSeriesID = ser.AnimeSeriesID).GetAwaiter().GetResult();

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpPost("Trakt/Comment/{traktID}/{isSpoiler}")]
    public CL_Response<bool> PostTraktCommentShow(string traktID, string commentText, bool isSpoiler)
    {
        return _traktHelper.PostCommentShow(traktID, commentText, isSpoiler);
    }

    [HttpPost("Trakt/LinkValidity/{slug}/{removeDBEntries}")]
    public bool CheckTraktLinkValidity(string slug, bool removeDBEntries)
    {
        try
        {
            return _traktHelper.CheckTraktValidity(slug, removeDBEntries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
        }

        return false;
    }

    [HttpGet("Trakt/CrossRef")]
    public List<CrossRef_AniDB_TraktV2> GetAllTraktCrossRefs()
    {
        try
        {
            return RepoFactory.CrossRef_AniDB_TraktV2.GetAll().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
        }

        return new List<CrossRef_AniDB_TraktV2>();
    }

    [HttpGet("Trakt/Comment/{animeID}")]
    public List<CL_Trakt_CommentUser> GetTraktCommentsForAnime(int animeID)
    {
        return new List<CL_Trakt_CommentUser>();
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
            _logger.LogError(ex, "Error in GetTraktDeviceCode: " + ex);
            return null;
        }
    }

    #endregion

    #region Other Cross Refs

    [HttpGet("WebCache/CrossRef/Other/{animeID}/{crossRefType}")]
    public CL_CrossRef_AniDB_Other_Response GetOtherAnimeCrossRefWebCache(int animeID, int crossRefType)
    {
        try
        {
            return new CL_CrossRef_AniDB_Other_Response();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return null;
        }
    }

    [HttpGet("Other/CrossRef/{animeID}/{crossRefType}")]
    public CrossRef_AniDB_Other GetOtherAnimeCrossRef(int animeID, int crossRefType)
    {
        try
        {
            return RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(animeID, (CrossRefType)crossRefType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return null;
        }
    }

    [HttpPost("Other/CrossRef/{animeID}/{id}/{crossRefType}")]
    public string LinkAniDBOther(int animeID, int id, int crossRefType)
    {
        try
        {
            var xrefType = (CrossRefType)crossRefType;

            switch (xrefType)
            {
                case CrossRefType.MovieDB:
                    _movieDBHelper.LinkAniDBMovieDB(animeID, id, false);
                    break;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
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

            var xrefType = (CrossRefType)crossRefType;
            switch (xrefType)
            {
                case CrossRefType.MovieDB:

                    // check if there are default images used associated
                    var images =
                        RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
                    foreach (var image in images)
                    {
                        if (image.ImageParentType == (int)ImageEntityType.MovieDB_FanArt ||
                            image.ImageParentType == (int)ImageEntityType.MovieDB_Poster)
                        {
                            RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                        }
                    }

                    _movieDBHelper.RemoveLinkAniDBMovieDB(animeID);
                    break;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
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
            var movieResults = _movieDBHelper.Search(criteria).Result;

            foreach (var res in movieResults)
            {
                results.Add(res.ToContract());
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return results;
        }
    }

    [HttpGet("MovieDB/Poster/{movieID?}")]
    public List<MovieDB_Poster> GetAllMovieDBPosters(int? movieID)
    {
        try
        {
            if (movieID.HasValue)
            {
                return RepoFactory.MovieDB_Poster.GetByMovieID(movieID.Value);
            }

            return RepoFactory.MovieDB_Poster.GetAllOriginal();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new List<MovieDB_Poster>();
        }
    }

    [HttpGet("MovieDB/Fanart/{movieID?}")]
    public List<MovieDB_Fanart> GetAllMovieDBFanart(int? movieID)
    {
        try
        {
            if (movieID.HasValue)
            {
                return RepoFactory.MovieDB_Fanart.GetByMovieID(movieID.Value);
            }

            return RepoFactory.MovieDB_Fanart.GetAllOriginal();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
            return new List<MovieDB_Fanart>();
        }
    }

    [HttpPost("MovieDB/Refresh/{movieID}")]
    public string UpdateMovieDBData(int movieD)
    {
        try
        {
            _movieDBHelper.UpdateMovieInfo(movieD, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
        }

        return string.Empty;
    }

    #endregion
}
