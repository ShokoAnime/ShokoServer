using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;
using JMMServer.Commands;
using System.IO;
using System.Net;
using BinaryNorthwest;
using JMMContracts;
using NHibernate;
using AniDBAPI;
using JMMServer.Providers.TraktTV.Contracts;

namespace JMMServer.Providers.TraktTV
{
    using global::JMMServer.Utilities;

	public class TraktTVHelper
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

        #region Helpers

        public static DateTime? GetDateFromUTCString(string sdate)
        {
            DateTime dt = DateTime.UtcNow;
            if (DateTime.TryParse(sdate, out dt))
            {
                return dt;
                //DateTime convertedDate = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                //return convertedDate.ToLocalTime();
            }

            return null;
        }

        private static int SendData(string uri, string json, string verb, Dictionary<string, string> headers, ref string webResponse)
        {
            int ret = 400;

            try
            {
                byte[] data = new UTF8Encoding().GetBytes(json);

                string msg = "Trakt SEND Data" + Environment.NewLine +
                            "Verb: " + verb + Environment.NewLine +
                            "uri: " + uri + Environment.NewLine +
                            "json: " + json + Environment.NewLine;
                logger.Trace(msg);

                var request = WebRequest.Create(uri) as HttpWebRequest;
                request.KeepAlive = true;

                request.Method = verb;
                request.ContentLength = data.Length;
                request.Timeout = 120000;
                request.ContentType = "application/json";
                request.UserAgent = "JMM";
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }

                // post to trakt
                Stream postStream = request.GetRequestStream();
                postStream.Write(data, 0, data.Length);

                // get the response
                var response = (HttpWebResponse)request.GetResponse();
                if (response == null) return 400;

                Stream responseStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(responseStream);
                string strResponse = reader.ReadToEnd();

                int statusCode = (int)response.StatusCode;

                // cleanup
                postStream.Close();
                responseStream.Close();
                reader.Close();
                response.Close();

                webResponse = strResponse;

                msg = "Trakt SEND Data - Response" + Environment.NewLine +
                            "Status Code: " + statusCode.ToString() + Environment.NewLine +
                            "Response: " + strResponse + Environment.NewLine;
                logger.Trace(msg);

                return statusCode;

            }
            catch (WebException webEx)
            {
                if (webEx.Status == WebExceptionStatus.ProtocolError)
                {
                    var response = webEx.Response as HttpWebResponse;
                    if (response != null)
                    {
                        logger.Error("Error in SendData: {0} - {1}", (int)response.StatusCode, webEx.ToString());
                        ret = (int)response.StatusCode;

                        try
                        {
                            Stream responseStream2 = response.GetResponseStream();
                            StreamReader reader2 = new StreamReader(responseStream2);
                            webResponse = reader2.ReadToEnd();
                            logger.Error("Error in SendData: {0}", webResponse);
                        }
                        catch { }
                    }
                    else
                    {
                        // no http status code available
                    }
                }
                Console.Write(webEx.ToString());
            }
            catch (Exception ex)
            {
                logger.Error("Error in SendData: {0}", ex.ToString());
            }
            finally
            {
            }

            return ret;
        }

        public static string GetFromTrakt(string uri)
        {
            var request = WebRequest.Create(uri) as HttpWebRequest;

            string msg = "Trakt GET Data" + Environment.NewLine +
                            "uri: " + uri + Environment.NewLine;
            logger.Trace(msg);

            request.KeepAlive = true;
            request.Method = "GET";
            request.ContentLength = 0;
            request.Timeout = 120000;
            request.ContentType = "application/json";
            request.UserAgent = "JMM";
            foreach (var header in BuildRequestHeaders())
            {
                request.Headers.Add(header.Key, header.Value);
            }

            try
            {
                WebResponse response = (HttpWebResponse)request.GetResponse();
                if (response == null) return null;

                Stream stream = response.GetResponseStream();
                StreamReader reader = new StreamReader(stream);
                string strResponse = reader.ReadToEnd();

                stream.Close();
                reader.Close();
                response.Close();

                msg = "Trakt GET Data - Response" + Environment.NewLine +
                            "Response: " + strResponse + Environment.NewLine;
                logger.Trace(msg);

                return strResponse;
            }
            catch (WebException e)
            {
                logger.Error("Error in GetFromTrakt: {0}", e.ToString());
                return null;
            }
            catch (Exception ex)
            {
                logger.Error("Error in GetFromTrakt: {0}", ex.ToString());
                return null;
            }
        }

        private static Dictionary<string, string> BuildRequestHeaders()
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();

            headers.Add("Authorization", string.Format("Bearer {0}", ServerSettings.Trakt_AuthToken));
            headers.Add("trakt-api-key", TraktConstants.ClientID);
            headers.Add("trakt-api-version", "2");

            return headers;
        }

        #endregion

        #region Authorization

        public static bool RefreshAuthToken()
        {
            try
            {
                if (string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken) ||
                    string.IsNullOrEmpty(ServerSettings.Trakt_RefreshToken))
                {
                    ServerSettings.Trakt_AuthToken = "";
                    ServerSettings.Trakt_RefreshToken = "";
                    ServerSettings.Trakt_TokenExpirationDate = "";

                    return false;
                }

                TraktV2RefreshToken token = new TraktV2RefreshToken();
                token.refresh_token = ServerSettings.Trakt_RefreshToken;

                string json = JSONHelper.Serialize<TraktV2RefreshToken>(token);
                Dictionary<string, string> headers = new Dictionary<string, string>();

                string retData = string.Empty;
                int response = SendData(TraktURIs.Oauth, json, "POST", headers, ref retData);
                if (response == TraktStatusCodes.Success || response == TraktStatusCodes.Success_Post)
                {
                    var loginResponse = retData.FromJSON<TraktAuthToken>();

                    // save the token to the config file to use for subsequent API calls
                    ServerSettings.Trakt_AuthToken = loginResponse.AccessToken;
                    ServerSettings.Trakt_RefreshToken = loginResponse.RefreshToken;

                    long createdAt = 0;
                    long validity = 0;

                    long.TryParse(loginResponse.CreatedAt, out createdAt);
                    long.TryParse(loginResponse.ExpiresIn, out validity);
                    long expireDate = createdAt + validity;

                    ServerSettings.Trakt_TokenExpirationDate = expireDate.ToString();

                    return true;
                }
                else
                {
                    ServerSettings.Trakt_AuthToken = "";
                    ServerSettings.Trakt_RefreshToken = "";
                    ServerSettings.Trakt_TokenExpirationDate = "";

                    return false;
                }

            }
            catch (Exception ex)
            {
                ServerSettings.Trakt_AuthToken = "";
                ServerSettings.Trakt_RefreshToken = "";
                ServerSettings.Trakt_TokenExpirationDate = "";

                logger.ErrorException("Error in TraktTVHelper.RefreshAuthToken: " + ex.ToString(), ex);
                return false;
            }
        }

        public static string EnterTraktPIN(string pin)
        {
            try
            {
                TraktAuthPIN obj = new TraktAuthPIN();
                obj.PINCode = pin;

                string json = JSONHelper.Serialize<TraktAuthPIN>(obj);
                Dictionary<string, string> headers = new Dictionary<string, string>();

                string retData = string.Empty;
                int response = SendData(TraktURIs.Oauth, json, "POST", headers, ref retData);
                if (response == TraktStatusCodes.Success || response == TraktStatusCodes.Success_Post)
                {
                    var loginResponse = retData.FromJSON<TraktAuthToken>();

                    // save the token to the config file to use for subsequent API calls
                    ServerSettings.Trakt_AuthToken = loginResponse.AccessToken;
                    ServerSettings.Trakt_RefreshToken = loginResponse.RefreshToken;

                    long createdAt = 0;
                    long validity = 0;

                    long.TryParse(loginResponse.CreatedAt, out createdAt);
                    long.TryParse(loginResponse.ExpiresIn, out validity);
                    long expireDate = createdAt + validity;

                    ServerSettings.Trakt_TokenExpirationDate = expireDate.ToString();

                    //MainWindow.UpdateTraktFriendInfo(true);

                    return "Success";
                }
                else
                {
                    ServerSettings.Trakt_AuthToken = "";
                    ServerSettings.Trakt_RefreshToken = "";
                    ServerSettings.Trakt_TokenExpirationDate = "";

                    return string.Format("Error returned from Trakt: {0}", response);
                }

            }
            catch (Exception ex)
            {
                ServerSettings.Trakt_AuthToken = "";
                ServerSettings.Trakt_RefreshToken = "";
                ServerSettings.Trakt_TokenExpirationDate = "";

                logger.ErrorException("Error in TraktTVHelper.TestUserLogin: " + ex.ToString(), ex);
                return ex.Message;
            }
        }

        #endregion

        #region Linking

        public static string LinkAniDBTrakt(int animeID, enEpisodeType aniEpType, int aniEpNumber, string traktID, int seasonNumber, int traktEpNumber, bool excludeFromWebCache)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return LinkAniDBTrakt(session, animeID, aniEpType, aniEpNumber, traktID, seasonNumber, traktEpNumber, excludeFromWebCache);
            }
        }

        public static string LinkAniDBTrakt(ISession session, int animeID, enEpisodeType aniEpType, int aniEpNumber, string traktID, int seasonNumber, int traktEpNumber, bool excludeFromWebCache)
        {
            CrossRef_AniDB_TraktV2Repository repCrossRef = new CrossRef_AniDB_TraktV2Repository();
            List<CrossRef_AniDB_TraktV2> xrefTemps = repCrossRef.GetByAnimeIDEpTypeEpNumber(session, animeID, (int)aniEpType, aniEpNumber);
            if (xrefTemps != null && xrefTemps.Count > 0)
            {
                foreach (CrossRef_AniDB_TraktV2 xrefTemp in xrefTemps)
                {
                    // delete the existing one if we are updating
                    TraktTVHelper.RemoveLinkAniDBTrakt(xrefTemp.AnimeID, (enEpisodeType)xrefTemp.AniDBStartEpisodeType, xrefTemp.AniDBStartEpisodeNumber,
                        xrefTemp.TraktID, xrefTemp.TraktSeasonNumber, xrefTemp.TraktStartEpisodeNumber);
                }
            }

            // check if we have this information locally
            // if not download it now
            Trakt_ShowRepository repSeries = new Trakt_ShowRepository();
            Trakt_Show traktShow = repSeries.GetByTraktSlug(traktID);
            if (traktShow == null)
            {
                // we download the series info here just so that we have the basic info in the
                // database before the queued task runs later
                TraktV2ShowExtended tvshow = GetShowInfoV2(traktID);
            }

            // download and update series info, episode info and episode images
            // will also download fanart, posters and wide banners
            // download fanart, posters
            DownloadAllImages(traktID);

            CrossRef_AniDB_TraktV2 xref = repCrossRef.GetByTraktID(session, traktID, seasonNumber, traktEpNumber, animeID, (int)aniEpType, aniEpNumber);
            if (xref == null)
                xref = new CrossRef_AniDB_TraktV2();

            xref.AnimeID = animeID;
            xref.AniDBStartEpisodeType = (int)aniEpType;
            xref.AniDBStartEpisodeNumber = aniEpNumber;

            xref.TraktID = traktID;
            xref.TraktSeasonNumber = seasonNumber;
            xref.TraktStartEpisodeNumber = traktEpNumber;
            if (traktShow != null)
                xref.TraktTitle = traktShow.Title;

            if (excludeFromWebCache)
                xref.CrossRefSource = (int)CrossRefSource.WebCache;
            else
                xref.CrossRefSource = (int)CrossRefSource.User;

            repCrossRef.Save(xref);

            StatsCache.Instance.UpdateUsingAnime(animeID);

            logger.Trace("Changed trakt association: {0}", animeID);

            if (!excludeFromWebCache && ServerSettings.WebCache_Trakt_Send)
            {
                CommandRequest_WebCacheSendXRefAniDBTrakt req = new CommandRequest_WebCacheSendXRefAniDBTrakt(xref.CrossRef_AniDB_TraktV2ID);
                req.Save();
            }

            return "";
        }

        public static void RemoveLinkAniDBTrakt(int animeID, enEpisodeType aniEpType, int aniEpNumber, string traktID, int seasonNumber, int traktEpNumber)
        {
            CrossRef_AniDB_TraktV2Repository repCrossRef = new CrossRef_AniDB_TraktV2Repository();
            CrossRef_AniDB_TraktV2 xref = repCrossRef.GetByTraktID(traktID, seasonNumber, traktEpNumber, animeID, (int)aniEpType, aniEpNumber);
            if (xref == null) return;

            repCrossRef.Delete(xref.CrossRef_AniDB_TraktV2ID);

            StatsCache.Instance.UpdateUsingAnime(animeID);

            if (ServerSettings.WebCache_Trakt_Send)
            {
                CommandRequest_WebCacheDeleteXRefAniDBTrakt req = new CommandRequest_WebCacheDeleteXRefAniDBTrakt(animeID, (int)aniEpType, aniEpNumber,
                    traktID, seasonNumber, traktEpNumber);
                req.Save();
            }
        }

        private static void GetDictTraktEpisodesAndSeasons(Trakt_Show show, ref Dictionary<int, Trakt_Episode> dictTraktEpisodes,
            ref Dictionary<int, Trakt_Episode> dictTraktSpecials, ref Dictionary<int, int> dictTraktSeasons)
        {
            dictTraktEpisodes = new Dictionary<int, Trakt_Episode>();
            dictTraktSpecials = new Dictionary<int, Trakt_Episode>();
            dictTraktSeasons = new Dictionary<int, int>();
            try
            {
                Trakt_EpisodeRepository repEps = new Trakt_EpisodeRepository();

                // create a dictionary of absolute episode numbers for trakt episodes
                // sort by season and episode number
                // ignore season 0, which is used for specials
                List<Trakt_Episode> eps = repEps.GetByShowID(show.Trakt_ShowID);

                List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
                sortCriteria.Add(new SortPropOrFieldAndDirection("Season", false, SortType.eInteger));
                sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeNumber", false, SortType.eInteger));
                eps = Sorting.MultiSort<Trakt_Episode>(eps, sortCriteria);

                int i = 1;
                int iSpec = 1;
                int lastSeason = -999;
                foreach (Trakt_Episode ep in eps)
                {
                    //if (ep.Season == 0) continue;
                    if (ep.Season > 0)
                    {
                        dictTraktEpisodes[i] = ep;
                        if (ep.Season != lastSeason)
                            dictTraktSeasons[ep.Season] = i;

                        i++;
                    }
                    else
                    {
                        dictTraktSpecials[iSpec] = ep;
                        if (ep.Season != lastSeason)
                            dictTraktSeasons[ep.Season] = iSpec;

                        iSpec++;
                    }

                    lastSeason = ep.Season;
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public static void ScanForMatches()
        {
            AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
            List<AnimeSeries> allSeries = repSeries.GetAll();

            CrossRef_AniDB_TraktV2Repository repCrossRef = new CrossRef_AniDB_TraktV2Repository();
            List<CrossRef_AniDB_TraktV2> allCrossRefs = repCrossRef.GetAll();
            List<int> alreadyLinked = new List<int>();
            foreach (CrossRef_AniDB_TraktV2 xref in allCrossRefs)
            {
                alreadyLinked.Add(xref.AnimeID);
            }

            foreach (AnimeSeries ser in allSeries)
            {
                if (alreadyLinked.Contains(ser.AniDB_ID)) continue;

                AniDB_Anime anime = ser.GetAnime();

                if (anime != null)
                    logger.Trace("Found anime without Trakt association: " + anime.MainTitle);

                if (anime.IsTraktLinkDisabled) continue;

                CommandRequest_TraktSearchAnime cmd = new CommandRequest_TraktSearchAnime(ser.AniDB_ID, false);
                cmd.Save();
            }

        }

        private static int? GetTraktEpisodeIdV2(AnimeEpisode ep, ref string traktID)
        {
            AniDB_Episode aniep = ep.AniDB_Episode;
            if (aniep == null) return null;

            AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
            AniDB_Anime anime = repAnime.GetByAnimeID(aniep.AnimeID);
            if (anime == null) return null;

            string traktShowID = string.Empty;

            int? traktEpisodeId = GetTraktEpisodeIdV2(anime, aniep, ref traktShowID);
            if (!traktEpisodeId.HasValue) return null;

            return GetTraktEpisodeIdV2(anime, aniep, ref traktShowID);

        }

        private static int? GetTraktEpisodeIdV2(AniDB_Anime anime, AniDB_Episode ep, ref string traktID)
        {
            TraktSummaryContainer traktSummary = new TraktSummaryContainer();
            traktSummary.Populate(anime.AnimeID);

            return GetTraktEpisodeIdV2(traktSummary, anime, ep, ref traktID);

        }

        private static int? GetTraktEpisodeIdV2(TraktSummaryContainer traktSummary, AniDB_Anime anime, AniDB_Episode ep, ref string traktID)
        {
            try
            {
                int? traktEpId = null;

                #region normal episodes
                // now do stuff to improve performance
                if (ep.EpisodeTypeEnum == enEpisodeType.Episode)
                {
                    if (traktSummary != null && traktSummary.CrossRefTraktV2 != null && traktSummary.CrossRefTraktV2.Count > 0)
                    {
                        // find the xref that is right
                        // relies on the xref's being sorted by season number and then episode number (desc)
                        List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
                        sortCriteria.Add(new SortPropOrFieldAndDirection("AniDBStartEpisodeNumber", true, SortType.eInteger));
                        List<CrossRef_AniDB_TraktV2> traktCrossRef = Sorting.MultiSort<CrossRef_AniDB_TraktV2>(traktSummary.CrossRefTraktV2, sortCriteria);

                        bool foundStartingPoint = false;
                        CrossRef_AniDB_TraktV2 xrefBase = null;
                        foreach (CrossRef_AniDB_TraktV2 xrefTrakt in traktCrossRef)
                        {
                            if (xrefTrakt.AniDBStartEpisodeType != (int)enEpisodeType.Episode) continue;
                            if (ep.EpisodeNumber >= xrefTrakt.AniDBStartEpisodeNumber)
                            {
                                foundStartingPoint = true;
                                xrefBase = xrefTrakt;
                                break;
                            }
                        }

                        // we have found the starting epiosde numbder from AniDB
                        // now let's check that the Trakt Season and Episode Number exist
                        if (foundStartingPoint)
                        {

                            Dictionary<int, int> dictTraktSeasons = null;
                            Dictionary<int, Trakt_Episode> dictTraktEpisodes = null;
                            foreach (TraktDetailsContainer det in traktSummary.TraktDetails.Values)
                            {
                                if (det.TraktID == xrefBase.TraktID)
                                {
                                    dictTraktSeasons = det.DictTraktSeasons;
                                    dictTraktEpisodes = det.DictTraktEpisodes;
                                    break;
                                }
                            }

                            if (dictTraktSeasons.ContainsKey(xrefBase.TraktSeasonNumber))
                            {
                                int episodeNumber = dictTraktSeasons[xrefBase.TraktSeasonNumber] + (ep.EpisodeNumber + xrefBase.TraktStartEpisodeNumber - 2) -
                                    (xrefBase.AniDBStartEpisodeNumber - 1);
                                if (dictTraktEpisodes.ContainsKey(episodeNumber))
                                {
                                    Trakt_Episode traktep = dictTraktEpisodes[episodeNumber];
                                    traktID = xrefBase.TraktID;
                                    traktEpId = traktep.TraktID;
                                }
                            }
                        }
                    }
                }
                #endregion

                #region special episodes
                if (ep.EpisodeTypeEnum == enEpisodeType.Special)
                {
                    // find the xref that is right
                    // relies on the xref's being sorted by season number and then episode number (desc)
                    List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
                    sortCriteria.Add(new SortPropOrFieldAndDirection("AniDBStartEpisodeNumber", true, SortType.eInteger));
                    List<CrossRef_AniDB_TraktV2> traktCrossRef = Sorting.MultiSort<CrossRef_AniDB_TraktV2>(traktSummary.CrossRefTraktV2, sortCriteria);

                    bool foundStartingPoint = false;
                    CrossRef_AniDB_TraktV2 xrefBase = null;
                    foreach (CrossRef_AniDB_TraktV2 xrefTrakt in traktCrossRef)
                    {
                        if (xrefTrakt.AniDBStartEpisodeType != (int)enEpisodeType.Special) continue;
                        if (ep.EpisodeNumber >= xrefTrakt.AniDBStartEpisodeNumber)
                        {
                            foundStartingPoint = true;
                            xrefBase = xrefTrakt;
                            break;
                        }
                    }

                    if (traktSummary != null && traktSummary.CrossRefTraktV2 != null && traktSummary.CrossRefTraktV2.Count > 0)
                    {
                        // we have found the starting epiosde numbder from AniDB
                        // now let's check that the Trakt Season and Episode Number exist
                        if (foundStartingPoint)
                        {

                            Dictionary<int, int> dictTraktSeasons = null;
                            Dictionary<int, Trakt_Episode> dictTraktEpisodes = null;
                            foreach (TraktDetailsContainer det in traktSummary.TraktDetails.Values)
                            {
                                if (det.TraktID == xrefBase.TraktID)
                                {
                                    dictTraktSeasons = det.DictTraktSeasons;
                                    dictTraktEpisodes = det.DictTraktEpisodes;
                                    break;
                                }
                            }

                            if (dictTraktSeasons.ContainsKey(xrefBase.TraktSeasonNumber))
                            {
                                int episodeNumber = dictTraktSeasons[xrefBase.TraktSeasonNumber] + (ep.EpisodeNumber + xrefBase.TraktStartEpisodeNumber - 2) -
                                    (xrefBase.AniDBStartEpisodeNumber - 1);
                                if (dictTraktEpisodes.ContainsKey(episodeNumber))
                                {
                                    Trakt_Episode traktep = dictTraktEpisodes[episodeNumber];
                                    traktID = xrefBase.TraktID;
                                    traktEpId = traktep.TraktID;
                                }
                            }
                        }
                    }
                }
                #endregion

                return traktEpId;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }


        #endregion

        #region Image Downloads

        /// <summary>
        /// Updates the followung
        /// 1. Series Info
        /// 2. Episode Info
        /// 3. Episode Images
        /// 4. Fanart, Poster Images
        /// </summary>
        /// <param name="seriesID"></param>
        /// <param name="forceRefresh"></param>
        public static void UpdateAllInfoAndImages(string traktID, bool forceRefresh)
        {
            // this will do the first 3 steps
            TraktV2ShowExtended tvShow = GetShowInfoV2(traktID);
            if (tvShow == null) return;

            try
            {
                //now download the images
                DownloadAllImages(traktID);
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in TraktTVHelper.UpdateAllInfoAndImages: " + ex.ToString(), ex);
            }
        }

        public static void DownloadAllImages(string traktID)
        {
            try
            {
                //now download the images
                Trakt_ShowRepository repShow = new Trakt_ShowRepository();
                Trakt_Show show = repShow.GetByTraktSlug(traktID);
                if (show == null) return;


                if (ServerSettings.Trakt_DownloadFanart)
                {
                    //download the fanart image for the show
                    Trakt_ImageFanartRepository repFanart = new Trakt_ImageFanartRepository();
                    Trakt_ImageFanart fanart = repFanart.GetByShowIDAndSeason(show.Trakt_ShowID, 1);
                    if (fanart != null)
                    {
                        if (!string.IsNullOrEmpty(fanart.FullImagePath))
                        {
                            if (!File.Exists(fanart.FullImagePath))
                            {
                                CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(fanart.Trakt_ImageFanartID, JMMImageType.Trakt_Fanart, false);
                                cmd.Save();
                            }
                        }
                    }
                }


                // download the posters for seasons
                Trakt_ImagePosterRepository repPosters = new Trakt_ImagePosterRepository();
                foreach (Trakt_Season season in show.Seasons)
                {
                    if (ServerSettings.Trakt_DownloadPosters)
                    {
                        Trakt_ImagePoster poster = repPosters.GetByShowIDAndSeason(season.Trakt_ShowID, season.Season);
                        if (poster != null)
                        {
                            if (!string.IsNullOrEmpty(poster.FullImagePath))
                            {
                                if (!File.Exists(poster.FullImagePath))
                                {
                                    CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(poster.Trakt_ImagePosterID, JMMImageType.Trakt_Poster, false);
                                    cmd.Save();
                                }
                            }
                        }
                    }

                    if (ServerSettings.Trakt_DownloadEpisodes)
                    {
                        // download the screenshots for episodes
                        foreach (Trakt_Episode ep in season.Episodes)
                        {
                            if (!string.IsNullOrEmpty(ep.FullImagePath))
                            {
                                if (!File.Exists(ep.FullImagePath))
                                {
                                    CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(ep.Trakt_EpisodeID, JMMImageType.Trakt_Episode, false);
                                    cmd.Save();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in TraktTVHelper.UpdateAllInfoAndImages: " + ex.ToString(), ex);
            }
        }

        #endregion

        #region Send Data to Trakt

        public static bool PostShoutShow(string traktSlug, string shoutText, bool isSpoiler, ref string returnMessage)
		{
			returnMessage = "";
			try
			{
                if (string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
				{
					returnMessage = "Trakt has not been authorized";
					return false;
				}

				if (string.IsNullOrEmpty(shoutText))
				{
					returnMessage = "Please enter text for your shout";
					return false;
				}

                TraktV2CommentShowPost comment = new TraktV2CommentShowPost();
                comment.Init(shoutText, isSpoiler, traktSlug);

                string json = JSONHelper.Serialize<TraktV2CommentShowPost>(comment);


                string retData = string.Empty;
                int response = SendData(TraktURIs.PostComment, json, "POST", BuildRequestHeaders(), ref retData);
                if (response == TraktStatusCodes.Success || response == TraktStatusCodes.Success_Post || response == TraktStatusCodes.Success_Delete)
                {
                    returnMessage = "Success";
                    return true;
                }
                else
                {
                    returnMessage = string.Format("{0} Error - {1}", response, retData);
                    return false;
                }
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.PostShoutShow: " + ex.ToString(), ex);
				returnMessage = ex.Message;
				return false;
			}

            return true;
		}

        public static void SyncEpisodeToTrakt(AnimeEpisode ep, TraktSyncType syncType)
        {
            try
            {
                if (string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                    return;

                string traktShowID = string.Empty;

                int? traktEpisodeId = GetTraktEpisodeIdV2(ep, ref traktShowID);
                if (!traktEpisodeId.HasValue) return;

                TraktV2SyncCollectionEpisodes sync = new TraktV2SyncCollectionEpisodes();
                sync.episodes = new List<TraktV2EpisodePost>();
                TraktV2EpisodePost epPost = new TraktV2EpisodePost();
                epPost.ids = new TraktV2EpisodeIds();
                epPost.ids.trakt = traktEpisodeId.Value;
                sync.episodes.Add(epPost);

                string json = JSONHelper.Serialize<TraktV2SyncCollectionEpisodes>(sync);

                Dictionary<string, string> headers = new Dictionary<string, string>();

                string url = TraktURIs.SyncCollectionAdd;
                switch (syncType)
                {
                    case TraktSyncType.CollectionAdd: url = TraktURIs.SyncCollectionAdd; break;
                    case TraktSyncType.CollectionRemove: url = TraktURIs.SyncCollectionRemove; break;
                    case TraktSyncType.HistoryAdd: url = TraktURIs.SyncHistoryAdd; break;
                    case TraktSyncType.HistoryRemove: url = TraktURIs.SyncHistoryRemove; break;
                }

                string retData = string.Empty;
                int response = SendData(url, json, "POST", BuildRequestHeaders(), ref retData);
                if (response == TraktStatusCodes.Success || response == TraktStatusCodes.Success_Post || response == TraktStatusCodes.Success_Delete)
                {
                    // if this was marking an episode as watched, and is successful, let's also add this to the user's collection
                    // this is because you can watch something without adding it to your collection, but in JMM it is always part of your collection
                    if (syncType == TraktSyncType.HistoryAdd)
                        response = SendData(TraktURIs.SyncCollectionAdd, json, "POST", BuildRequestHeaders(), ref retData);

                    // also if we have removed from our collection, set to un-watched
                    if (syncType == TraktSyncType.CollectionRemove)
                        response = SendData(TraktURIs.SyncHistoryRemove, json, "POST", BuildRequestHeaders(), ref retData);

                }

            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in TraktTVHelper.MarkEpisodeWatched: " + ex.ToString(), ex);
            }


        }

        #endregion

        #region Get Data From Trakt

        public static List<TraktV2SearchShowResult> SearchShowV2(string criteria)
        {
            List<TraktV2SearchShowResult> results = new List<TraktV2SearchShowResult>();

            try
            {
                // replace spaces with a + symbo
                criteria = criteria.Replace(' ', '+');

                // Search for a series
                string url = string.Format(TraktURIs.Search, criteria, TraktSearchType.show);
                logger.Trace("Search Trakt Show: {0}", url);

                // Search for a series
                string json = GetFromTrakt(url);

                if (json.Trim().Length == 0) return new List<TraktV2SearchShowResult>();

                var result = json.FromJSONArray<TraktV2SearchShowResult>();
                if (result == null) return null;

                return new List<TraktV2SearchShowResult>(result);

                // save this data for later use
                //foreach (TraktTVShow tvshow in results)
                //	SaveExtendedShowInfo(tvshow);
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in SearchSeries: " + ex.ToString(), ex);
            }

            return null;
        }

        public static TraktV2ShowExtended GetShowInfoV2(int tvDBID)
        {
            return GetShowInfoV2(tvDBID.ToString());
        }

        public static TraktV2ShowExtended GetShowInfoV2(string traktID)
        {
            TraktV2ShowExtended resultShow = null;

            try
            {
                string url = string.Format(TraktURIs.ShowSummary, traktID);
                logger.Trace("GetShowInfo: {0}", url);

                // Search for a series
                string json = GetFromTrakt(url);

                if (json.Trim().Length == 0) return null;

                resultShow = json.FromJSON<TraktV2ShowExtended>();
                if (resultShow == null) return null;

                // if we got the show info, also download the seaon info
                url = string.Format(TraktURIs.ShowSeasons, traktID);
                logger.Trace("GetSeasonInfo: {0}", url);
                json = GetFromTrakt(url);

                List<TraktV2Season> seasons = new List<TraktV2Season>();
                if (json.Trim().Length > 0)
                {
                    var resultSeasons = json.FromJSONArray<TraktV2Season>();
                    if (resultShow != null)
                    {
                        foreach (TraktV2Season season in resultSeasons)
                            seasons.Add(season);
                    }
                }

                // save this data to the DB for use later
                SaveExtendedShowInfoV2(resultShow, seasons);

            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in TraktTVHelper.GetShowInfo: " + ex.ToString(), ex);
                return null;
            }

            return resultShow;
        }

        public static void SaveExtendedShowInfoV2(TraktV2ShowExtended tvshow, List<TraktV2Season> seasons)
        {
            try
            {
                // save this data to the DB for use later
                Trakt_ImageFanartRepository repFanart = new Trakt_ImageFanartRepository();
                Trakt_ShowRepository repShows = new Trakt_ShowRepository();
                Trakt_Show show = repShows.GetByTraktSlug(tvshow.ids.slug);
                if (show == null)
                    show = new Trakt_Show();

                show.Populate(tvshow);
                repShows.Save(show);


                if (tvshow.images != null && tvshow.images.fanart != null)
                {
                    if (!string.IsNullOrEmpty(tvshow.images.fanart.full))
                    {
                        Trakt_ImageFanart fanart = repFanart.GetByShowIDAndSeason(show.Trakt_ShowID, 1);
                        if (fanart == null)
                        {
                            fanart = new Trakt_ImageFanart();
                            fanart.Enabled = 0;
                        }

                        fanart.ImageURL = tvshow.images.fanart.full;
                        fanart.Season = 1;
                        fanart.Trakt_ShowID = show.Trakt_ShowID;
                        repFanart.Save(fanart);
                    }
                }


                // save the seasons
                Trakt_SeasonRepository repSeasons = new Trakt_SeasonRepository();
                Trakt_EpisodeRepository repEpisodes = new Trakt_EpisodeRepository();
                Trakt_ImagePosterRepository repPosters = new Trakt_ImagePosterRepository();


                foreach (TraktV2Season sea in seasons)
                {
                    Trakt_Season season = repSeasons.GetByShowIDAndSeason(show.Trakt_ShowID, sea.number);
                    if (season == null)
                        season = new Trakt_Season();

                    season.Season = sea.number;
                    season.URL = string.Format(TraktURIs.WebsiteSeason, show.TraktID, sea.number);
                    season.Trakt_ShowID = show.Trakt_ShowID;
                    repSeasons.Save(season);

                    if (sea.images != null && sea.images.poster != null)
                    {
                        if (!string.IsNullOrEmpty(sea.images.poster.full))
                        {
                            Trakt_ImagePoster poster = repPosters.GetByShowIDAndSeason(show.Trakt_ShowID, season.Season);
                            if (poster == null)
                            {
                                poster = new Trakt_ImagePoster();
                                poster.Enabled = 0;
                            }

                            poster.ImageURL = sea.images.poster.full;
                            poster.Season = season.Season;
                            poster.Trakt_ShowID = show.Trakt_ShowID;
                            repPosters.Save(poster);
                        }
                    }

                    foreach (TraktV2Episode ep in sea.episodes)
                    {
                        Trakt_Episode episode = repEpisodes.GetByShowIDSeasonAndEpisode(show.Trakt_ShowID, ep.season, ep.number);
                        if (episode == null)
                            episode = new Trakt_Episode();

                        Console.Write(ep.ids.trakt);

                        if (ep.images.screenshot != null)
                            episode.EpisodeImage = ep.images.screenshot.full;
                        else
                            episode.EpisodeImage = string.Empty;

                        episode.TraktID = ep.ids.trakt;
                        episode.EpisodeNumber = ep.number;
                        episode.Overview = string.Empty; // this is now part of a separate API call for V2, we get this info from TvDB anyway
                        episode.Season = ep.season;
                        episode.Title = ep.title;
                        episode.URL = string.Format(TraktURIs.WebsiteEpisode, show.TraktID, ep.season, ep.number);
                        episode.Trakt_ShowID = show.Trakt_ShowID;
                        repEpisodes.Save(episode);
                    }
                }


            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in TraktTVHelper.SaveExtendedShowInfo: " + ex.ToString(), ex);
            }
        }

        public static List<TraktV2Comment> GetShowShoutsV2(int animeID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetShowShoutsV2(session, animeID);
            }
        }

        public static List<TraktV2Comment> GetShowShoutsV2(ISession session, int animeID)
        {
            List<TraktV2Comment> ret = new List<TraktV2Comment>();
            try
            {
                if (string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                    return ret;

                CrossRef_AniDB_TraktV2Repository repXrefTrakt = new CrossRef_AniDB_TraktV2Repository();
                List<CrossRef_AniDB_TraktV2> traktXRefs = repXrefTrakt.GetByAnimeID(session, animeID);
                if (traktXRefs == null || traktXRefs.Count == 0) return null;

                // get a unique list of trakt id's
                List<string> ids = new List<string>();
                foreach (CrossRef_AniDB_TraktV2 xref in traktXRefs)
                {
                    if (!ids.Contains(xref.TraktID))
                        ids.Add(xref.TraktID);
                }

                foreach (string id in ids)
                {
                    bool morePages = true;
                    int curPage = 0;

                    while (morePages)
                    {
                        curPage++;
                        string url = string.Format(TraktURIs.ShowComments, id, curPage, TraktConstants.PaginationLimit);
                        logger.Trace("GetShowShouts: {0}", url);

                        string json = GetFromTrakt(url);

                        if (json.Trim().Length == 0) 
                            return null;

                        var resultComments = json.FromJSONArray<TraktV2Comment>();
                        if (resultComments != null)
                        {
                            List<TraktV2Comment> thisComments = new List<TraktV2Comment>(resultComments);
                            ret.AddRange(thisComments);

                            if (thisComments.Count == TraktConstants.PaginationLimit)
                                morePages = true;
                            else
                                morePages = false;
                        }
                        else
                            morePages = false;
                        
                    }
                    
                }

            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in TraktTVHelper.GetShowShouts: " + ex.ToString(), ex);
            }

            return ret;
        }

        public static List<TraktV2Follower> GetFriendsV2()
        {
            List<TraktV2Follower> friends = new List<TraktV2Follower>();

            try
            {
                if (string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                    return friends;

                string url = TraktURIs.GetUserFriends;
                logger.Trace("GetFollowers: {0}", url);

                string json = GetFromTrakt(url);
                if (json.Trim().Length == 0) return null;

                var resultFollowers = json.FromJSONArray<TraktV2Follower>();

                Trakt_ShowRepository repShows = new Trakt_ShowRepository();
                Trakt_EpisodeRepository repEpisodes = new Trakt_EpisodeRepository();
                Trakt_FriendRepository repFriends = new Trakt_FriendRepository();

                foreach (TraktV2Follower friend in resultFollowers)
                {
                    Trakt_Friend traktFriend = repFriends.GetByUsername(friend.user.username);
                    if (traktFriend == null)
                        traktFriend = new Trakt_Friend();

                    traktFriend.Populate(friend.user);
                    repFriends.Save(traktFriend);

                    // get a watched history for each friend
                    url = string.Format(TraktURIs.GetUserHistory, friend.user.username);
                    logger.Trace("GetUserHistory: {0}", url);

                    json = GetFromTrakt(url);
                    if (json.Trim().Length == 0) continue;

                    var resultHistory = json.FromJSONArray<TraktV2UserEpisodeHistory>();

                    /*
                    foreach (TraktV2UserEpisodeHistory wtch in resultHistory)
                    {
                        if (wtch.episode != null && wtch.show != null)
                        {

                            Trakt_Show show = repShows.GetByTraktID(wtch.show.ids.slug);
                            if (show == null)
                            {
                                show = new Trakt_Show();
                                show.Populate(wtch.show);
                                repShows.Save(show);
                            }

                            Trakt_Episode episode = repEpisodes.GetByShowIDSeasonAndEpisode(show.Trakt_ShowID, wtch.episode.season, wtch.episode.number);
                            if (episode == null)
                                episode = new Trakt_Episode();

                            episode.Populate(wtch.episode, show.Trakt_ShowID);
                            repEpisodes.Save(episode);

                            if (!string.IsNullOrEmpty(episode.FullImagePath))
                            {
                                bool fileExists = File.Exists(episode.FullImagePath);
                                if (!fileExists)
                                {
                                    CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(episode.Trakt_EpisodeID, JMMImageType.Trakt_Episode, false);
                                    cmd.Save();
                                }
                            }
                        }
                    }*/
                }



                //Contract_Trakt_Friend fr = friends[0].ToContract();

            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in TraktTVHelper.GetFriends: " + ex.ToString(), ex);
                return friends;
            }

            return friends;
        }

        #endregion


        public static List<TraktTVShowUserCollectionWatched> GetUserCollection()
		{
			List<TraktTVShowUserCollectionWatched> results = new List<TraktTVShowUserCollectionWatched>();

			try
			{
                string url = string.Format(Constants.TraktTvURLs.URLUserLibraryShowsCollection, Constants.TraktTvURLs.APIKey, ServerSettings.Trakt_AuthToken);
				logger.Trace("Trakt User Collection: {0}", url);

				// Search for a series
				string json = Utils.DownloadWebPage(url);

				if (json.Trim().Length == 0) return new List<TraktTVShowUserCollectionWatched>();

				results = JSONHelper.Deserialize<List<TraktTVShowUserCollectionWatched>>(json);

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in SearchSeries: " + ex.ToString(), ex);
			}

			return results;
		}

		public static List<TraktTVShowUserCollectionWatched> GetUserWatched()
		{
			List<TraktTVShowUserCollectionWatched> results = new List<TraktTVShowUserCollectionWatched>();

			try
			{
                string url = string.Format(Constants.TraktTvURLs.URLUserLibraryShowsWatched, Constants.TraktTvURLs.APIKey, ServerSettings.Trakt_AuthToken);
				logger.Trace("Trakt User Collection Watched: {0}", url);

				// Search for a series
				string json = Utils.DownloadWebPage(url);

				if (json.Trim().Length == 0) return new List<TraktTVShowUserCollectionWatched>();

				results = JSONHelper.Deserialize<List<TraktTVShowUserCollectionWatched>>(json);

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in SearchSeries: " + ex.ToString(), ex);
			}

			return results;
		}

		public static void UpdateAllInfo()
		{
            CrossRef_AniDB_TraktV2Repository repCrossRef = new CrossRef_AniDB_TraktV2Repository();
            List<CrossRef_AniDB_TraktV2> allCrossRefs = repCrossRef.GetAll();
            foreach (CrossRef_AniDB_TraktV2 xref in allCrossRefs)
			{
				CommandRequest_TraktUpdateInfoAndImages cmd = new CommandRequest_TraktUpdateInfoAndImages(xref.TraktID);
				cmd.Save();
			}

		}

		public static void SyncCollectionToTrakt_Series(AnimeSeries series)
		{
			try
			{
				// check that we have at least one user nominated for Trakt
				JMMUserRepository repUsers = new JMMUserRepository();
				List<JMMUser> traktUsers = repUsers.GetTraktUsers();
				if (traktUsers.Count == 0) return;

                AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
                AniDB_Anime anime = repAnime.GetByAnimeID(series.AniDB_ID);
                if (anime == null) return;

                TraktSummaryContainer traktSummary = new TraktSummaryContainer();
                traktSummary.Populate(series.AniDB_ID);
                if (traktSummary.CrossRefTraktV2 == null || traktSummary.CrossRefTraktV2.Count == 0) return;

				foreach (AnimeEpisode ep in series.GetAnimeEpisodes())
				{
					if (ep.GetVideoLocals().Count > 0)
					{
                        // let's check if this episode has a user record against it
                        // if it does, it means a user has watched it

                        AnimeEpisode_User userRecord = null;
                        foreach (JMMUser juser in traktUsers)
                        {
                            userRecord = ep.GetUserRecord(juser.JMMUserID);
                            if (userRecord != null) break;
                        }

                        if (userRecord != null)
                        {
                            // adding to history will also add it to the collection
                            SyncEpisodeToTrakt(ep, TraktSyncType.HistoryAdd);
                        }
                        else
                        {
                            // lets' add it to the collection
                            SyncEpisodeToTrakt(ep, TraktSyncType.CollectionAdd);
                        }
							
					}
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.SyncCollectionToTrakt_Series: " + ex.ToString(), ex);
			}
		}

		public static void SyncCollectionToTrakt()
		{
			try
			{
                if (string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken)) return;

				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				List<AnimeSeries> allSeries = repSeries.GetAll();

				foreach (AnimeSeries series in allSeries)
				{
					//SyncCollectionToTrakt_Series(series);
					CommandRequest_TraktSyncCollectionSeries cmd = new CommandRequest_TraktSyncCollectionSeries(series.AnimeSeriesID, series.GetAnime().MainTitle);
					cmd.Save();
				}

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.SyncCollectionToTrakt: " + ex.ToString(), ex);
			}
		}
 
    }
}
