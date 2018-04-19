using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Azure;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Models.Server;
using Shoko.Models.TvDB;
using Shoko.Server.Commands;
using Shoko.Server.Commands.MAL;
using Shoko.Server.Commands.TvDB;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.MyAnimeList;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TraktTV.Contracts;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;
using TvDbSharper.Dto;

namespace Shoko.Server
{
    public partial class ShokoServiceImplementation : IShokoServer
    {
        public CL_AniDB_AnimeCrossRefs GetCrossRefDetails(int animeID)
        {
            CL_AniDB_AnimeCrossRefs result = new CL_AniDB_AnimeCrossRefs
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
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                    if (anime == null) return result;

                    var xrefs = RepoFactory.CrossRef_AniDB_TvDB.GetV2LinksFromAnime(animeID);

                    // TvDB
                    result.CrossRef_AniDB_TvDB = xrefs;

                    foreach (TvDB_Episode ep in anime.GetTvDBEpisodes())
                        result.TvDBEpisodes.Add(ep);

                    foreach (var xref in xrefs.DistinctBy(a => a.TvDBID))
                    {
                        TvDB_Series ser = RepoFactory.TvDB_Series.GetByTvDBID(xref.TvDBID);
                        if (ser != null)
                            result.TvDBSeries.Add(ser);

                        foreach (TvDB_ImageFanart fanart in RepoFactory.TvDB_ImageFanart.GetBySeriesID(xref.TvDBID))
                            result.TvDBImageFanarts.Add(fanart);

                        foreach (TvDB_ImagePoster poster in RepoFactory.TvDB_ImagePoster.GetBySeriesID(xref.TvDBID))
                            result.TvDBImagePosters.Add(poster);

                        foreach (TvDB_ImageWideBanner banner in RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(xref
                            .TvDBID))
                            result.TvDBImageWideBanners.Add(banner);
                    }

                    // Trakt


                    foreach (CrossRef_AniDB_TraktV2 xref in anime.GetCrossRefTraktV2(session))
                    {
                        result.CrossRef_AniDB_Trakt.Add(xref);

                        Trakt_Show show = RepoFactory.Trakt_Show.GetByTraktSlug(session, xref.TraktID);
                        if (show != null) result.TraktShows.Add(show.ToClient());
                    }


                    // MovieDB
                    CrossRef_AniDB_Other xrefMovie = anime.GetCrossRefMovieDB();
                    result.CrossRef_AniDB_MovieDB = xrefMovie;


                    result.MovieDBMovie = anime.GetMovieDBMovie();


                    foreach (MovieDB_Fanart fanart in anime.GetMovieDBFanarts())
                    {
                        if (fanart.ImageSize.Equals(Shoko.Models.Constants.MovieDBImageSize.Original,
                            StringComparison.InvariantCultureIgnoreCase))
                            result.MovieDBFanarts.Add(fanart);
                    }

                    foreach (MovieDB_Poster poster in anime.GetMovieDBPosters())
                    {
                        if (poster.ImageSize.Equals(Shoko.Models.Constants.MovieDBImageSize.Original,
                            StringComparison.InvariantCultureIgnoreCase))
                            result.MovieDBPosters.Add(poster);
                    }

                    // MAL
                    List<CrossRef_AniDB_MAL> xrefMAL = anime.GetCrossRefMAL();
                    if (xrefMAL == null)
                        result.CrossRef_AniDB_MAL = null;
                    else
                    {
                        result.CrossRef_AniDB_MAL = new List<CrossRef_AniDB_MAL>();
                        foreach (CrossRef_AniDB_MAL xrefTemp in xrefMAL)
                            result.CrossRef_AniDB_MAL.Add(xrefTemp);
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return result;
            }
        }

        #region Web Cache Admin

        public bool IsWebCacheAdmin()
        {
            try
            {
                string res = AzureWebAPI.Admin_AuthUser();
                return string.IsNullOrEmpty(res);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return false;
            }
        }

        public Azure_AnimeLink Admin_GetRandomLinkForApproval(int linkType)
        {
            try
            {
                AzureLinkType lType = (AzureLinkType) linkType;
                Azure_AnimeLink link = null;

                switch (lType)
                {
                    case AzureLinkType.TvDB:
                        link = AzureWebAPI.Admin_GetRandomTvDBLinkForApproval();
                        break;
                    case AzureLinkType.Trakt:
                        link = AzureWebAPI.Admin_GetRandomTraktLinkForApproval();
                        break;
                }


                return link;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public List<Azure_AdminMessage> GetAdminMessages()
        {
            try
            {
                return ServerInfo.Instance.AdminMessages?.ToList() ?? new List<Azure_AdminMessage>();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<Azure_AdminMessage>();
            }
        }

        #region Admin - TvDB

        public string ApproveTVDBCrossRefWebCache(int crossRef_AniDB_TvDBId)
        {
            try
            {
                return AzureWebAPI.Admin_Approve_CrossRefAniDBTvDB(crossRef_AniDB_TvDBId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public string RevokeTVDBCrossRefWebCache(int crossRef_AniDB_TvDBId)
        {
            try
            {
                return AzureWebAPI.Admin_Revoke_CrossRefAniDBTvDB(crossRef_AniDB_TvDBId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        /// <summary>
        /// Sends the current user's TvDB links to the web cache, and then admin approves them
        /// </summary>
        /// <returns></returns>
        public string UseMyTvDBLinksWebCache(int animeID)
        {
            try
            {
                // Get all the links for this user and anime
                List<CrossRef_AniDB_TvDB> xrefs = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(animeID);
                if (xrefs == null) return "No Links found to use";

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return "Anime not found";

                // make sure the user doesn't alreday have links
                List<Azure_CrossRef_AniDB_TvDB> results =
                    AzureWebAPI.Admin_Get_CrossRefAniDBTvDB(animeID);
                bool foundLinks = false;
                if (results != null)
                {
                    foreach (Azure_CrossRef_AniDB_TvDB xref in results)
                    {
                        if (xref.Username.Equals(ServerSettings.AniDB_Username,
                            StringComparison.InvariantCultureIgnoreCase))
                        {
                            foundLinks = true;
                            break;
                        }
                    }
                }
                if (foundLinks) return "Links already exist, please approve them instead";

                // send the links to the web cache
                foreach (CrossRef_AniDB_TvDB xref in xrefs)
                {
                    AzureWebAPI.Send_CrossRefAniDBTvDB(xref.ToV2Model(), anime.MainTitle);
                }

                // now get the links back from the cache and approve them
                results = AzureWebAPI.Admin_Get_CrossRefAniDBTvDB(animeID);
                if (results != null)
                {
                    List<Azure_CrossRef_AniDB_TvDB> linksToApprove =
                        new List<Azure_CrossRef_AniDB_TvDB>();
                    foreach (Azure_CrossRef_AniDB_TvDB xref in results)
                    {
                        if (xref.Username.Equals(ServerSettings.AniDB_Username,
                            StringComparison.InvariantCultureIgnoreCase))
                            linksToApprove.Add(xref);
                    }

                    foreach (Azure_CrossRef_AniDB_TvDB xref in linksToApprove)
                    {
                        AzureWebAPI.Admin_Approve_CrossRefAniDBTvDB(
                            xref.CrossRef_AniDB_TvDBV2ID);
                    }
                    return "Success";
                }
                else
                    return "Failure to send links to web cache";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        #endregion

        #region Admin - Trakt

        public string ApproveTraktCrossRefWebCache(int crossRef_AniDB_TraktId)
        {
            try
            {
                return AzureWebAPI.Admin_Approve_CrossRefAniDBTrakt(crossRef_AniDB_TraktId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public string RevokeTraktCrossRefWebCache(int crossRef_AniDB_TraktId)
        {
            try
            {
                return AzureWebAPI.Admin_Revoke_CrossRefAniDBTrakt(crossRef_AniDB_TraktId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        /// <summary>
        /// Sends the current user's Trakt links to the web cache, and then admin approves them
        /// </summary>
        /// <returns></returns>
        public string UseMyTraktLinksWebCache(int animeID)
        {
            try
            {
                // Get all the links for this user and anime
                List<CrossRef_AniDB_TraktV2> xrefs = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(animeID);
                if (xrefs == null) return "No Links found to use";

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return "Anime not found";

                // make sure the user doesn't alreday have links
                List<Azure_CrossRef_AniDB_Trakt> results =
                    AzureWebAPI.Admin_Get_CrossRefAniDBTrakt(animeID);
                bool foundLinks = false;
                if (results != null)
                {
                    foreach (Azure_CrossRef_AniDB_Trakt xref in results)
                    {
                        if (xref.Username.Equals(ServerSettings.AniDB_Username,
                            StringComparison.InvariantCultureIgnoreCase))
                        {
                            foundLinks = true;
                            break;
                        }
                    }
                }
                if (foundLinks) return "Links already exist, please approve them instead";

                // send the links to the web cache
                foreach (CrossRef_AniDB_TraktV2 xref in xrefs)
                {
                    AzureWebAPI.Send_CrossRefAniDBTrakt(xref, anime.MainTitle);
                }

                // now get the links back from the cache and approve them
                results = AzureWebAPI.Admin_Get_CrossRefAniDBTrakt(animeID);
                if (results != null)
                {
                    List<Azure_CrossRef_AniDB_Trakt> linksToApprove =
                        new List<Azure_CrossRef_AniDB_Trakt>();
                    foreach (Azure_CrossRef_AniDB_Trakt xref in results)
                    {
                        if (xref.Username.Equals(ServerSettings.AniDB_Username,
                            StringComparison.InvariantCultureIgnoreCase))
                            linksToApprove.Add(xref);
                    }

                    foreach (Azure_CrossRef_AniDB_Trakt xref in linksToApprove)
                    {
                        AzureWebAPI.Admin_Approve_CrossRefAniDBTrakt(
                            xref.CrossRef_AniDB_TraktV2ID);
                    }
                    return "Success";
                }
                else
                    return "Failure to send links to web cache";

                //return JMMServer.Providers.Azure.AzureWebAPI.Admin_Approve_CrossRefAniDBTrakt(crossRef_AniDB_TraktId);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        #endregion

        #endregion

        #region TvDB

        public string UpdateTvDBData(int seriesID)
        {
            try
            {
                CommandRequest_TvDBUpdateSeries updateseries = new CommandRequest_TvDBUpdateSeries(seriesID, true);
                updateseries.Save();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return string.Empty;
        }

        public List<TvDB_Language> GetTvDBLanguages()
        {
            try
            {
                return TvDBApiHelper.GetLanguages();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return new List<TvDB_Language>();
        }

        public List<Azure_CrossRef_AniDB_TvDB> GetTVDBCrossRefWebCache(int animeID, bool isAdmin)
        {
            try
            {
                if (isAdmin)
                    return AzureWebAPI.Admin_Get_CrossRefAniDBTvDB(animeID);
                else
                    return AzureWebAPI.Get_CrossRefAniDBTvDB(animeID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<Azure_CrossRef_AniDB_TvDB>();
            }
        }


        public List<CrossRef_AniDB_TvDBV2> GetTVDBCrossRefV2(int animeID)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_TvDB.GetV2LinksFromAnime(animeID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public List<CrossRef_AniDB_TvDB_Episode_Override> GetTVDBCrossRefEpisode(int animeID)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAnimeID(animeID).ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }


        public List<TVDB_Series_Search_Response> SearchTheTvDB(string criteria)
        {
            try
            {
                return TvDBApiHelper.SearchSeries(criteria);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<TVDB_Series_Search_Response>();
            }
        }


        public List<int> GetSeasonNumbersForSeries(int seriesID)
        {
            List<int> seasonNumbers = new List<int>();
            try
            {
                // refresh data from TvDB
                TvDBApiHelper.UpdateSeriesInfoAndImages(seriesID, true, false);

                seasonNumbers = RepoFactory.TvDB_Episode.GetSeasonNumbersForSeries(seriesID);

                return seasonNumbers;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return seasonNumbers;
            }
        }

        public string LinkAniDBTvDB(CrossRef_AniDB_TvDBV2 link)
        {
            try
            {
                CrossRef_AniDB_TvDB xref = RepoFactory.CrossRef_AniDB_TvDB.GetByAniDBAndTvDBID(link.AnimeID, link.TvDBID);

                if (xref != null && link.IsAdditive)
                {
                    string msg = $"You have already linked Anime ID {xref.AniDBID} to this TvDB show/season/ep";
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref.AniDBID);
                    if (anime != null)
                        msg =
                            $"You have already linked Anime {anime.MainTitle} ({xref.AniDBID}) to this TvDB show/season/ep";
                    return msg;
                }

                // we don't need to proactively remove the link here anymore, as all links are removed when it is not marked as additive
                CommandRequest_LinkAniDBTvDB cmdRequest =
                    new CommandRequest_LinkAniDBTvDB(link.AnimeID, link.TvDBID, link.IsAdditive);
                cmdRequest.Save();

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string LinkTvDBUsingWebCacheLinks(List<CrossRef_AniDB_TvDBV2> links)
        {
            try
            {
                if (links.Count == 0) return "No links were given in the request. This is a bug.";

                var link = links[0];

                var existingLinks = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(link.AnimeID);
                RepoFactory.CrossRef_AniDB_TvDB.Delete(existingLinks);
                RepoFactory.CrossRef_AniDB_TvDB_Episode.DeleteAllUnverifiedLinksForAnime(link.AnimeID);

                // we don't need to proactively remove the link here anymore, as all links are removed when it is not marked as additive
                CommandRequest_LinkAniDBTvDB cmdRequest =
                    new CommandRequest_LinkAniDBTvDB(link.AnimeID, link.TvDBID, link.IsAdditive);
                cmdRequest.Save();

                var overrides = TvDBLinkingHelper.GetSpecialsOverridesFromLegacy(links);
                foreach (var episodeOverride in overrides)
                {
                    var exists =
                        RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAniDBAndTvDBEpisodeIDs(
                            episodeOverride.AniDBEpisodeID, episodeOverride.TvDBEpisodeID);
                    if (exists != null) continue;
                    RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.Save(episodeOverride);
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string LinkAniDBTvDBEpisode(int aniDBID, int tvDBID)
        {
            try
            {
                TvDBApiHelper.LinkAniDBTvDBEpisode(aniDBID, tvDBID);

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        /// <summary>
        /// Removes all tvdb links for one anime
        /// </summary>
        /// <param name="animeID"></param>
        /// <returns></returns>
        public string RemoveLinkAniDBTvDBForAnime(int animeID)
        {
            try
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

                if (ser == null) return "Could not find Series for Anime!";

                List<CrossRef_AniDB_TvDB> xrefs = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(animeID);
                if (xrefs == null) return string.Empty;

                foreach (CrossRef_AniDB_TvDB xref in xrefs)
                {
                    // check if there are default images used associated
                    List<AniDB_Anime_DefaultImage> images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
                    foreach (AniDB_Anime_DefaultImage image in images)
                    {
                        if (image.ImageParentType == (int) ImageEntityType.TvDB_Banner ||
                            image.ImageParentType == (int) ImageEntityType.TvDB_Cover ||
                            image.ImageParentType == (int) ImageEntityType.TvDB_FanArt)
                        {
                            if (image.ImageParentID == xref.TvDBID)
                                RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                        }
                    }

                    TvDBApiHelper.RemoveLinkAniDBTvDB(xref.AniDBID, xref.TvDBID);
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string RemoveLinkAniDBTvDB(CrossRef_AniDB_TvDBV2 link)
        {
            try
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(link.AnimeID);

                if (ser == null) return "Could not find Series for Anime!";

                // check if there are default images used associated
                List<AniDB_Anime_DefaultImage> images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(link.AnimeID);
                foreach (AniDB_Anime_DefaultImage image in images)
                {
                    if (image.ImageParentType == (int) ImageEntityType.TvDB_Banner ||
                        image.ImageParentType == (int) ImageEntityType.TvDB_Cover ||
                        image.ImageParentType == (int) ImageEntityType.TvDB_FanArt)
                    {
                        if (image.ImageParentID == link.TvDBID)
                            RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                    }
                }

                TvDBApiHelper.RemoveLinkAniDBTvDB(link.AnimeID, link.TvDBID);

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string RemoveLinkAniDBTvDBEpisode(int aniDBEpisodeID, int tvDBEpisodeID)
        {
            try
            {
                AniDB_Episode ep = RepoFactory.AniDB_Episode.GetByEpisodeID(aniDBEpisodeID);

                if (ep == null) return "Could not find Episode";

                CrossRef_AniDB_TvDB_Episode_Override xref =
                    RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAniDBAndTvDBEpisodeIDs(aniDBEpisodeID,
                        tvDBEpisodeID);
                if (xref == null) return "Could not find Link!";


                RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.Delete(xref.CrossRef_AniDB_TvDB_Episode_OverrideID);

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public List<TvDB_ImagePoster> GetAllTvDBPosters(int? tvDBID)
        {
            List<TvDB_ImagePoster> allImages = new List<TvDB_ImagePoster>();
            try
            {
                if (tvDBID.HasValue)
                    return RepoFactory.TvDB_ImagePoster.GetBySeriesID(tvDBID.Value);
                else
                    return RepoFactory.TvDB_ImagePoster.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<TvDB_ImagePoster>();
            }
        }

        public List<TvDB_ImageWideBanner> GetAllTvDBWideBanners(int? tvDBID)
        {
            try
            {
                if (tvDBID.HasValue)
                    return RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(tvDBID.Value);
                else
                    return RepoFactory.TvDB_ImageWideBanner.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<TvDB_ImageWideBanner>();
            }
        }

        public List<TvDB_ImageFanart> GetAllTvDBFanart(int? tvDBID)
        {
            try
            {
                if (tvDBID.HasValue)
                    return RepoFactory.TvDB_ImageFanart.GetBySeriesID(tvDBID.Value);
                else
                    return RepoFactory.TvDB_ImageFanart.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<TvDB_ImageFanart>();
            }
        }

        public List<TvDB_Episode> GetAllTvDBEpisodes(int? tvDBID)
        {
            try
            {
                if (tvDBID.HasValue)
                    return RepoFactory.TvDB_Episode.GetBySeriesID(tvDBID.Value);
                else
                    return RepoFactory.TvDB_Episode.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<TvDB_Episode>();
            }
        }

        #endregion

        #region Trakt

        public List<Trakt_Episode> GetAllTraktEpisodes(int? traktShowID)
        {
            try
            {
                if (traktShowID.HasValue)
                    return RepoFactory.Trakt_Episode.GetByShowID(traktShowID.Value).ToList();
                else
                    return RepoFactory.Trakt_Episode.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<Trakt_Episode>();
            }
        }

        public List<Trakt_Episode> GetAllTraktEpisodesByTraktID(string traktID)
        {
            try
            {
                Trakt_Show show = RepoFactory.Trakt_Show.GetByTraktSlug(traktID);
                if (show != null)
                    return GetAllTraktEpisodes(show.Trakt_ShowID);

                return new List<Trakt_Episode>();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<Trakt_Episode>();
            }
        }

        public List<Azure_CrossRef_AniDB_Trakt> GetTraktCrossRefWebCache(int animeID, bool isAdmin)
        {
            try
            {
                if (isAdmin)
                    return AzureWebAPI.Admin_Get_CrossRefAniDBTrakt(animeID);
                else
                    return AzureWebAPI.Get_CrossRefAniDBTrakt(animeID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<Azure_CrossRef_AniDB_Trakt>();
            }
        }

        public string LinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID, int seasonNumber,
            int traktEpNumber, int? crossRef_AniDB_TraktV2ID)
        {
            try
            {
                if (crossRef_AniDB_TraktV2ID.HasValue)
                {
                    CrossRef_AniDB_TraktV2 xrefTemp =
                        RepoFactory.CrossRef_AniDB_TraktV2.GetByID(crossRef_AniDB_TraktV2ID.Value);
                    // delete the existing one if we are updating
                    TraktTVHelper.RemoveLinkAniDBTrakt(xrefTemp.AnimeID, (EpisodeType) xrefTemp.AniDBStartEpisodeType,
                        xrefTemp.AniDBStartEpisodeNumber,
                        xrefTemp.TraktID, xrefTemp.TraktSeasonNumber, xrefTemp.TraktStartEpisodeNumber);
                }

                CrossRef_AniDB_TraktV2 xref = RepoFactory.CrossRef_AniDB_TraktV2.GetByTraktID(traktID, seasonNumber,
                    traktEpNumber, animeID,
                    aniEpType,
                    aniEpNumber);
                if (xref != null)
                {
                    string msg = string.Format("You have already linked Anime ID {0} to this Trakt show/season/ep",
                        xref.AnimeID);
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref.AnimeID);
                    if (anime != null)
                    {
                        msg = string.Format("You have already linked Anime {0} ({1}) to this Trakt show/season/ep",
                            anime.MainTitle,
                            xref.AnimeID);
                    }
                    return msg;
                }

                return TraktTVHelper.LinkAniDBTrakt(animeID, (EpisodeType) aniEpType, aniEpNumber, traktID,
                    seasonNumber,
                    traktEpNumber, false);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }


        public List<CrossRef_AniDB_TraktV2> GetTraktCrossRefV2(int animeID)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(animeID).Cast<CrossRef_AniDB_TraktV2>().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public List<CrossRef_AniDB_Trakt_Episode> GetTraktCrossRefEpisode(int animeID)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_Trakt_Episode.GetByAnimeID(animeID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public List<CL_TraktTVShowResponse> SearchTrakt(string criteria)
        {
            List<CL_TraktTVShowResponse> results = new List<CL_TraktTVShowResponse>();
            try
            {
                List<TraktV2SearchShowResult> traktResults = TraktTVHelper.SearchShowV2(criteria);

                foreach (TraktV2SearchShowResult res in traktResults)
                    results.Add(res.ToContract());

                return results;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return results;
            }
        }

        public string RemoveLinkAniDBTraktForAnime(int animeID)
        {
            try
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

                if (ser == null) return "Could not find Series for Anime!";

                // check if there are default images used associated
                List<AniDB_Anime_DefaultImage> images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
                foreach (AniDB_Anime_DefaultImage image in images)
                {
                    if (image.ImageParentType == (int) ImageEntityType.Trakt_Fanart ||
                        image.ImageParentType == (int) ImageEntityType.Trakt_Poster)
                    {
                        RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                    }
                }

                foreach (CrossRef_AniDB_TraktV2 xref in RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(animeID))
                {
                    TraktTVHelper.RemoveLinkAniDBTrakt(animeID, (EpisodeType) xref.AniDBStartEpisodeType,
                        xref.AniDBStartEpisodeNumber,
                        xref.TraktID, xref.TraktSeasonNumber, xref.TraktStartEpisodeNumber);
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string RemoveLinkAniDBTrakt(int animeID, int aniEpType, int aniEpNumber, string traktID,
            int traktSeasonNumber,
            int traktEpNumber)
        {
            try
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

                if (ser == null) return "Could not find Series for Anime!";

                // check if there are default images used associated
                List<AniDB_Anime_DefaultImage> images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
                foreach (AniDB_Anime_DefaultImage image in images)
                {
                    if (image.ImageParentType == (int) ImageEntityType.Trakt_Fanart ||
                        image.ImageParentType == (int) ImageEntityType.Trakt_Poster)
                    {
                        RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                    }
                }

                TraktTVHelper.RemoveLinkAniDBTrakt(animeID, (EpisodeType) aniEpType, aniEpNumber,
                    traktID, traktSeasonNumber, traktEpNumber);

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public List<int> GetSeasonNumbersForTrakt(string traktID)
        {
            List<int> seasonNumbers = new List<int>();
            try
            {
                // refresh show info including season numbers from trakt
                TraktV2ShowExtended tvshow = TraktTVHelper.GetShowInfoV2(traktID);

                Trakt_Show show = RepoFactory.Trakt_Show.GetByTraktSlug(traktID);
                if (show == null) return seasonNumbers;

                foreach (Trakt_Season season in show.GetSeasons())
                    seasonNumbers.Add(season.Season);

                return seasonNumbers;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return seasonNumbers;
            }
        }

        public CL_Response<bool> TraktFriendRequestDeny(string friendUsername)
        {
            return new CL_Response<bool> {Result = false};
            /*
            try
            {
                return TraktTVHelper.FriendRequestDeny(friendUsername, ref returnMessage);
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error in TraktFriendRequestDeny: " + ex.ToString());
                returnMessage = ex.Message;
                return false;
            }*/
        }

        public CL_Response<bool> TraktFriendRequestApprove(string friendUsername)
        {
            return new CL_Response<bool> {Result = false};
            /*
            try
            {
                return TraktTVHelper.FriendRequestApprove(friendUsername, ref returnMessage);
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error in TraktFriendRequestDeny: " + ex.ToString());
                returnMessage = ex.Message;
                return false;
            }*/
        }

        public int TraktScrobble(int animeId, int type, int progress, int status)
        {
            try
            {
                Providers.TraktTV.ScrobblePlayingStatus statusTraktV2 = Providers.TraktTV.ScrobblePlayingStatus.Start;

                switch (status)
                {
                    case (int)Providers.TraktTV.ScrobblePlayingStatus.Start:
                        statusTraktV2 = Providers.TraktTV.ScrobblePlayingStatus.Start;
                        break;
                    case (int)Providers.TraktTV.ScrobblePlayingStatus.Pause:
                        statusTraktV2 = Providers.TraktTV.ScrobblePlayingStatus.Pause;
                        break;
                    case (int)Providers.TraktTV.ScrobblePlayingStatus.Stop:
                        statusTraktV2 = Providers.TraktTV.ScrobblePlayingStatus.Stop;
                        break;
                }

                bool isValidProgress = float.TryParse(progress.ToString(), out float progressTrakt);

                if (isValidProgress)
                {
                    switch (type)
                    {
                        // Movie
                        case (int) Providers.TraktTV.ScrobblePlayingType.movie:
                            return Providers.TraktTV.TraktTVHelper.Scrobble(
                                Providers.TraktTV.ScrobblePlayingType.movie, animeId.ToString(),
                                statusTraktV2, progressTrakt);
                        // TV episode
                        case (int) Providers.TraktTV.ScrobblePlayingType.episode:
                            return Providers.TraktTV.TraktTVHelper.Scrobble(
                                Providers.TraktTV.ScrobblePlayingType.episode,
                                animeId.ToString(), statusTraktV2, progressTrakt);
                    }
                }
                return 500;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return 500;
            }
        }

        public string UpdateTraktData(string traktD)
        {
            try
            {
                TraktTVHelper.UpdateAllInfo(traktD);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return string.Empty;
        }

        public string SyncTraktSeries(int animeID)
        {
            try
            {
                if (!ServerSettings.Trakt_IsEnabled) return string.Empty;

                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                if (ser == null) return "Could not find Anime Series";

                CommandRequest_TraktSyncCollectionSeries cmd =
                    new CommandRequest_TraktSyncCollectionSeries(ser.AnimeSeriesID,
                        ser.GetSeriesName());
                cmd.Save();

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public CL_Response<bool> PostTraktCommentShow(string traktID, string commentText, bool isSpoiler)
        {
            return TraktTVHelper.PostCommentShow(traktID, commentText, isSpoiler);
        }

        public bool CheckTraktLinkValidity(string slug, bool removeDBEntries)
        {
            try
            {
                return TraktTVHelper.CheckTraktValidity(slug, removeDBEntries);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return false;
        }

        public List<CrossRef_AniDB_TraktV2> GetAllTraktCrossRefs()
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_TraktV2.GetAll().Cast<CrossRef_AniDB_TraktV2>().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return new List<CrossRef_AniDB_TraktV2>();
        }

        public List<CL_Trakt_CommentUser> GetTraktCommentsForAnime(int animeID)
        {
            List<CL_Trakt_CommentUser> comments = new List<CL_Trakt_CommentUser>();

            try
            {
                List<TraktV2Comment> commentsTemp = TraktTVHelper.GetShowCommentsV2(animeID);
                if (commentsTemp == null || commentsTemp.Count == 0) return comments;

                foreach (TraktV2Comment sht in commentsTemp)
                {
                    CL_Trakt_CommentUser comment = new CL_Trakt_CommentUser();

                    Trakt_Friend traktFriend = RepoFactory.Trakt_Friend.GetByUsername(sht.user.username);

                    // user details
                    comment.User = new CL_Trakt_User();
                    if (traktFriend == null)
                        comment.User.Trakt_FriendID = 0;
                    else
                        comment.User.Trakt_FriendID = traktFriend.Trakt_FriendID;

                    comment.User.Username = sht.user.username;
                    comment.User.Full_name = sht.user.name;

                    // comment details
                    comment.Comment = new CL_Trakt_Comment
                    {
                        CommentType = (int)TraktActivityType.Show, // episode or show
                        Text = sht.comment,
                        Spoiler = sht.spoiler,
                        Inserted = sht.CreatedAtDate,

                        // urls
                        Comment_Url = string.Format(TraktURIs.WebsiteComment, sht.id)
                    };
                    comments.Add(comment);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return comments;
        }

        public string EnterTraktPIN(string pin)
        {
            try
            {
                return TraktTVHelper.EnterTraktPIN(pin);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in EnterTraktPIN: " + ex.ToString());
                return ex.Message;
            }
        }

        public CL_TraktDeviceCode GetTraktDeviceCode()
        {
            try
            {
                var response = TraktTVHelper.GetTraktDeviceCode();
                return new CL_TraktDeviceCode
                {
                    VerificationUrl = response.VerificationUrl,
                    UserCode = response.UserCode
                };
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in GetTraktDeviceCode: " + ex.ToString());
                return null;
            }
        }

        #endregion

        #region MAL

        public CL_CrossRef_AniDB_MAL_Response GetMALCrossRefWebCache(int animeID)
        {
            try
            {
                return AzureWebAPI.Get_CrossRefAniDBMAL(animeID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public List<CL_MALAnime_Response> SearchMAL(string criteria)
        {
            List<CL_MALAnime_Response> results = new List<CL_MALAnime_Response>();
            try
            {
                anime malResults = MALHelper.SearchAnimesByTitle(criteria);

                foreach (animeEntry res in malResults.entry)
                    results.Add(res.ToContract());

                return results;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return results;
            }
        }


        public string LinkAniDBMAL(int animeID, int malID, string malTitle, int epType, int epNumber)
        {
            try
            {
                CrossRef_AniDB_MAL xrefTemp = RepoFactory.CrossRef_AniDB_MAL.GetByMALID(malID);
                if (xrefTemp != null)
                {
                    string animeName = string.Empty;
                    try
                    {
                        SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xrefTemp.AnimeID);
                        if (anime != null) animeName = anime.MainTitle;
                    }
                    catch
                    {
                    }
                    return
                        $"Not using MAL link as this MAL ID ({malID}) is already in use by {xrefTemp.AnimeID} ({animeName})";
                }

                MALHelper.LinkAniDBMAL(animeID, malID, malTitle, epType, epNumber, false);

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string LinkAniDBMALUpdated(int animeID, int malID, string malTitle, int oldEpType, int oldEpNumber,
            int newEpType, int newEpNumber)
        {
            try
            {
                CrossRef_AniDB_MAL xrefTemp = RepoFactory.CrossRef_AniDB_MAL
                    .GetByAnimeConstraint(animeID, oldEpType, oldEpNumber).FirstOrDefault(a => a.MALID == malID);
                if (xrefTemp == null)
                    return $"Could not find MAL link ({animeID}/{oldEpType}/{oldEpNumber})";

                RepoFactory.CrossRef_AniDB_MAL.Delete(xrefTemp);

                return LinkAniDBMAL(animeID, malID, malTitle, newEpType, newEpNumber);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }


        public string RemoveLinkAniDBMAL(int animeID, int malID, int epType, int epNumber)
        {
            try
            {
                MALHelper.RemoveLinkAniDBMAL(animeID, malID, epType, epNumber);

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string TestMALLogin()
        {
            try
            {
                if (MALHelper.VerifyCredentials())
                    return string.Empty;

                return "Login is not valid";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TestMALLogin: " + ex.ToString());
                return ex.Message;
            }
        }

        public void SyncMALUpload()
        {
            CommandRequest_MALUploadStatusToMAL cmd = new CommandRequest_MALUploadStatusToMAL();
            cmd.Save();
        }

        public void SyncMALDownload()
        {
            CommandRequest_MALDownloadStatusFromMAL cmd = new CommandRequest_MALDownloadStatusFromMAL();
            cmd.Save();
        }

        #endregion

        #region Other Cross Refs

        public CL_CrossRef_AniDB_Other_Response GetOtherAnimeCrossRefWebCache(int animeID, int crossRefType)
        {
            try
            {
                return AzureWebAPI.Get_CrossRefAniDBOther(animeID, (CrossRefType) crossRefType);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public CrossRef_AniDB_Other GetOtherAnimeCrossRef(int animeID, int crossRefType)
        {
            try
            {
                return RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(animeID, (CrossRefType) crossRefType);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public string LinkAniDBOther(int animeID, int movieID, int crossRefType)
        {
            try
            {
                CrossRefType xrefType = (CrossRefType) crossRefType;

                switch (xrefType)
                {
                    case CrossRefType.MovieDB:
                        MovieDBHelper.LinkAniDBMovieDB(animeID, movieID, false);
                        break;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string RemoveLinkAniDBOther(int animeID, int crossRefType)
        {
            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);

                if (anime == null) return "Could not find Anime!";

                CrossRefType xrefType = (CrossRefType) crossRefType;
                switch (xrefType)
                {
                    case CrossRefType.MovieDB:

                        // check if there are default images used associated
                        List<AniDB_Anime_DefaultImage> images =
                            RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(animeID);
                        foreach (AniDB_Anime_DefaultImage image in images)
                        {
                            if (image.ImageParentType == (int) ImageEntityType.MovieDB_FanArt ||
                                image.ImageParentType == (int) ImageEntityType.MovieDB_Poster)
                            {
                                RepoFactory.AniDB_Anime_DefaultImage.Delete(image.AniDB_Anime_DefaultImageID);
                            }
                        }
                        MovieDBHelper.RemoveLinkAniDBMovieDB(animeID);
                        break;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        #endregion

        #region MovieDB

        public List<CL_MovieDBMovieSearch_Response> SearchTheMovieDB(string criteria)
        {
            List<CL_MovieDBMovieSearch_Response> results = new List<CL_MovieDBMovieSearch_Response>();
            try
            {
                List<MovieDB_Movie_Result> movieResults = MovieDBHelper.Search(criteria);

                foreach (MovieDB_Movie_Result res in movieResults)
                    results.Add(res.ToContract());

                return results;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return results;
            }
        }

        public List<MovieDB_Poster> GetAllMovieDBPosters(int? movieID)
        {
            try
            {
                if (movieID.HasValue)
                    return RepoFactory.MovieDB_Poster.GetByMovieID(movieID.Value);
                else
                    return RepoFactory.MovieDB_Poster.GetAllOriginal();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<MovieDB_Poster>();
            }
        }

        public List<MovieDB_Fanart> GetAllMovieDBFanart(int? movieID)
        {
            try
            {
                if (movieID.HasValue)
                    return RepoFactory.MovieDB_Fanart.GetByMovieID(movieID.Value);
                else
                    return RepoFactory.MovieDB_Fanart.GetAllOriginal();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<MovieDB_Fanart>();
            }
        }

        public string UpdateMovieDBData(int movieD)
        {
            try
            {
                MovieDBHelper.UpdateMovieInfo(movieD, true);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return string.Empty;
        }

        #endregion
    }
}
