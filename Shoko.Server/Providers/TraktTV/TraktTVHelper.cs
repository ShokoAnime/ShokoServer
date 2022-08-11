using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NHibernate;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV.Contracts;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Providers.TraktTV
{
    public static class TraktTVHelper
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        #region Helpers

        public static DateTime? GetDateFromUTCString(string sdate)
        {
            if (DateTime.TryParse(sdate, out var dt))
            {
                return dt;
                //DateTime convertedDate = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                //return convertedDate.ToLocalTime();
            }

            return null;
        }

        private static int SendData(string uri, string json, string verb, Dictionary<string, string> headers,
            ref string webResponse)
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

                var request = (HttpWebRequest) WebRequest.Create(uri);
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
                var response = (HttpWebResponse) request.GetResponse();

                Stream responseStream = response.GetResponseStream();
                if (responseStream == null) return ret;

                StreamReader reader = new StreamReader(responseStream);
                string strResponse = reader.ReadToEnd();

                int statusCode = (int) response.StatusCode;

                // cleanup
                postStream.Close();
                responseStream.Close();
                reader.Close();
                response.Close();

                webResponse = strResponse;

                msg = "Trakt SEND Data - Response" + Environment.NewLine +
                      "Status Code: " + statusCode + Environment.NewLine +
                      "Response: " + strResponse + Environment.NewLine;
                logger.Trace(msg);

                return statusCode;
            }
            catch (WebException webEx)
            {
                if (webEx.Status == WebExceptionStatus.ProtocolError)
                {
                    if (webEx.Response is HttpWebResponse response)
                    {
                        logger.Error($"Error in SendData: {(int) response.StatusCode} - {webEx}");
                        ret = (int) response.StatusCode;

                        try
                        {
                            Stream responseStream2 = response.GetResponseStream();
                            if (responseStream2 == null) return ret;
                            StreamReader reader2 = new StreamReader(responseStream2);
                            webResponse = reader2.ReadToEnd();
                            logger.Error($"Error in SendData: {webResponse}");
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }
                logger.Error(webEx);
            }
            catch (Exception ex)
            {
                logger.Error($"Error in SendData: {ex}");
            }

            return ret;
        }

        public static string GetFromTrakt(string uri)
        {
            int retCode = 400;
            return GetFromTrakt(uri, ref retCode);
        }

        public static string GetFromTrakt(string uri, ref int traktCode)
        {
            var request = (HttpWebRequest) WebRequest.Create(uri);

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
                WebResponse response = (HttpWebResponse) request.GetResponse();
                var httpResponse = (HttpWebResponse)response;
                traktCode = (int)httpResponse.StatusCode;
                Stream stream = response.GetResponseStream();
                if (stream == null) return null;

                StreamReader reader = new StreamReader(stream);
                string strResponse = reader.ReadToEnd();

                stream.Close();
                reader.Close();
                response.Close();

                // log the response unless it is Full Collection or Full Watched as this data is way too big
                if (!uri.Equals(TraktURIs.GetWatchedShows, StringComparison.InvariantCultureIgnoreCase) &&
                    !uri.Equals(TraktURIs.GetCollectedShows, StringComparison.InvariantCultureIgnoreCase))
                {
                    msg = "Trakt GET Data - Response" + Environment.NewLine +
                          "Response: " + strResponse + Environment.NewLine;
                    logger.Trace(msg);
                }

                return strResponse;
            }
            catch (WebException e)
            {
                logger.Error("Error in GetFromTrakt: {0}", e);

                var httpResponse = (HttpWebResponse) e.Response;
                traktCode = (int) httpResponse.StatusCode;

                return null;
            }
            catch (Exception ex)
            {
                logger.Error($"Error in GetFromTrakt: {ex}");
                return null;
            }
        }

        private static Dictionary<string, string> BuildRequestHeaders()
        {
            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                {"Authorization", $"Bearer {ServerSettings.Instance.TraktTv.AuthToken}"},
                {"trakt-api-key", TraktConstants.ClientID},
                {"trakt-api-version", "2"}
            };


            return headers;
        }

        #endregion

        #region Authorization

        public static void RefreshAuthToken()
        {
            try
            {
                if (!ServerSettings.Instance.TraktTv.Enabled ||
                    string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken) ||
                    string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.RefreshToken))
                {
                    ServerSettings.Instance.TraktTv.AuthToken = string.Empty;
                    ServerSettings.Instance.TraktTv.RefreshToken = string.Empty;
                    ServerSettings.Instance.TraktTv.TokenExpirationDate = string.Empty;

                    return;
                }

                TraktV2RefreshToken token = new TraktV2RefreshToken
                {
                    refresh_token = ServerSettings.Instance.TraktTv.RefreshToken
                };
                string json = JSONHelper.Serialize(token);
                Dictionary<string, string> headers = new Dictionary<string, string>();

                string retData = string.Empty;
                int response = SendData(TraktURIs.Oauth, json, "POST", headers, ref retData);
                if (response == TraktStatusCodes.Success || response == TraktStatusCodes.Success_Post)
                {
                    var loginResponse = retData.FromJSON<TraktAuthToken>();

                    // save the token to the config file to use for subsequent API calls
                    ServerSettings.Instance.TraktTv.AuthToken = loginResponse.AccessToken;
                    ServerSettings.Instance.TraktTv.RefreshToken = loginResponse.RefreshToken;

                    long.TryParse(loginResponse.CreatedAt, out long createdAt);
                    long.TryParse(loginResponse.ExpiresIn, out long validity);
                    long expireDate = createdAt + validity;

                    ServerSettings.Instance.TraktTv.TokenExpirationDate = expireDate.ToString();

                    return;
                }

                ServerSettings.Instance.TraktTv.AuthToken = string.Empty;
                ServerSettings.Instance.TraktTv.RefreshToken = string.Empty;
                ServerSettings.Instance.TraktTv.TokenExpirationDate = string.Empty;
            }
            catch (Exception ex)
            {
                ServerSettings.Instance.TraktTv.AuthToken = string.Empty;
                ServerSettings.Instance.TraktTv.RefreshToken = string.Empty;
                ServerSettings.Instance.TraktTv.TokenExpirationDate = string.Empty;

                logger.Error(ex, "Error in TraktTVHelper.RefreshAuthToken: " + ex);
            }
            finally
            {
                ServerSettings.Instance.SaveSettings();
            }
        }

        #endregion

        #region New Authorization

        /*
         *  Trakt Auth Flow
         *  
         *  1. Generate codes. Your app calls /oauth/device/code to generate new codes. Save this entire response for later use.
         *  2. Display the code. Display the user_code and instruct the user to visit the verification_url on their computer or mobile device.
         *  3. Poll for authorization. Poll the /oauth/device/token method to see if the user successfully authorizes your app. 
         *     Use the device_code and poll at the interval (in seconds) to check if the user has authorized your app.
         *     Use expires_in to stop polling after that many seconds, and gracefully instruct the user to restart the process. 
         *     It is important to poll at the correct interval and also stop polling when expired.
         *     Status Codes
         *     This method will send various HTTP status codes that you should handle accordingly.
         *     Code 	Description
         *     200 	Success - save the access_token
         *     400 	Pending - waiting for the user to authorize your app
         *     404 	Not Found - invalid device_code
         *     409 	Already Used - user already approved this code
         *     410 	Expired - the tokens have expired, restart the process
         *     418 	Denied - user explicitly denied this code
         *     429 	Slow Down - your app is polling too quickly
         *  4. Successful authorization. 
         *     When you receive a 200 success response, save the access_token so your app can authenticate the user in methods that require it. 
         *     The access_token is valid for 3 months.
         */

        public static TraktAuthDeviceCodeToken GetTraktDeviceCode()
        {
            try
            {
                var obj = new TraktAuthDeviceCode();
                string json = JSONHelper.Serialize(obj);
                Dictionary<string, string> headers = new Dictionary<string, string>();

                string retData = string.Empty;
                int response = SendData(TraktURIs.OAuthDeviceCode, json, "POST", headers, ref retData);
                if (response != TraktStatusCodes.Success && response != TraktStatusCodes.Success_Post)
                {
                    throw new Exception($"Error returned from Trakt: {response}");
                }

                var deviceCode = retData.FromJSON<TraktAuthDeviceCodeToken>();

                Task.Run(() => { TraktAuthPollDeviceToken(deviceCode); });

                return deviceCode;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TraktTVHelper.GetTraktDeviceCode: " + ex);
                throw;
            }
        }

        private static void TraktAuthPollDeviceToken(TraktAuthDeviceCodeToken deviceCode)
        {
            if (deviceCode == null)
            {
                return;
            }
            var task = Task.Run(() => { TraktAutoDeviceTokenWorker(deviceCode); });
            if (!task.Wait(TimeSpan.FromSeconds(deviceCode.ExpiresIn)))
            {
                logger.Error("Error in TraktTVHelper.TraktAuthPollDeviceToken: Timed out");
            }
        }

        private static void TraktAutoDeviceTokenWorker(TraktAuthDeviceCodeToken deviceCode)
        {
            try
            {
                var pollInterval = TimeSpan.FromSeconds(deviceCode.Interval);
                var obj = new TraktAuthDeviceCodePoll
                {
                    DeviceCode = deviceCode.DeviceCode
                };
                string json = JSONHelper.Serialize(obj);
                Dictionary<string, string> headers = new Dictionary<string, string>();
                while (true)
                {
                    Thread.Sleep(pollInterval);

                    headers.Clear();

                    string retData = string.Empty;
                    int response = SendData(TraktURIs.OAuthDeviceToken, json, "POST", headers, ref retData);
                    if (response == TraktStatusCodes.Success)
                    {
                        var tokenResponse = retData.FromJSON<TraktAuthToken>();
                        ServerSettings.Instance.TraktTv.AuthToken = tokenResponse.AccessToken;
                        ServerSettings.Instance.TraktTv.RefreshToken = tokenResponse.RefreshToken;

                        long.TryParse(tokenResponse.CreatedAt, out long createdAt);
                        long.TryParse(tokenResponse.ExpiresIn, out long validity);
                        long expireDate = createdAt + validity;

                        ServerSettings.Instance.TraktTv.TokenExpirationDate = expireDate.ToString();
                        ServerSettings.Instance.SaveSettings();
                        break;
                    }
                    if (response == TraktStatusCodes.Rate_Limit_Exceeded)
                    {
                        //Temporarily increase poll interval
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                    }
                    if (response != TraktStatusCodes.Bad_Request && response != TraktStatusCodes.Rate_Limit_Exceeded)
                    {
                        throw new Exception($"Error returned from Trakt: {response}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TraktTVHelper.TraktAuthDeviceCodeToken: " + ex);
                throw;
            }
        }

        #endregion

        #region Linking

        public static string LinkAniDBTrakt(int animeID, EpisodeType aniEpType, int aniEpNumber, string traktID,
            int seasonNumber, int traktEpNumber, bool excludeFromWebCache)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return LinkAniDBTrakt(session, animeID, aniEpType, aniEpNumber, traktID, seasonNumber, traktEpNumber,
                    excludeFromWebCache);
            }
        }

        public static string LinkAniDBTrakt(ISession session, int animeID, EpisodeType aniEpType, int aniEpNumber,
            string traktID, int seasonNumber, int traktEpNumber, bool excludeFromWebCache)
        {
            List<CrossRef_AniDB_TraktV2> xrefTemps = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeIDEpTypeEpNumber(
                session, animeID,
                (int) aniEpType,
                aniEpNumber);
            if (xrefTemps != null && xrefTemps.Count > 0)
            {
                foreach (CrossRef_AniDB_TraktV2 xrefTemp in xrefTemps)
                {
                    // delete the existing one if we are updating
                    RemoveLinkAniDBTrakt(xrefTemp.AnimeID, (EpisodeType) xrefTemp.AniDBStartEpisodeType,
                        xrefTemp.AniDBStartEpisodeNumber,
                        xrefTemp.TraktID, xrefTemp.TraktSeasonNumber, xrefTemp.TraktStartEpisodeNumber);
                }
            }

            // check if we have this information locally
            // if not download it now
            Trakt_Show traktShow = RepoFactory.Trakt_Show.GetByTraktSlug(traktID);
            if (traktShow == null)
            {
                // we download the series info here just so that we have the basic info in the
                // database before the queued task runs later
                GetShowInfoV2(traktID);
                traktShow = RepoFactory.Trakt_Show.GetByTraktSlug(traktID);
            }

            // download and update series info, episode info and episode images

            CrossRef_AniDB_TraktV2 xref = RepoFactory.CrossRef_AniDB_TraktV2.GetByTraktID(session, traktID,
                                              seasonNumber, traktEpNumber,
                                              animeID,
                                              (int) aniEpType, aniEpNumber) ?? new CrossRef_AniDB_TraktV2();

            xref.AnimeID = animeID;
            xref.AniDBStartEpisodeType = (int) aniEpType;
            xref.AniDBStartEpisodeNumber = aniEpNumber;

            xref.TraktID = traktID;
            xref.TraktSeasonNumber = seasonNumber;
            xref.TraktStartEpisodeNumber = traktEpNumber;
            if (traktShow != null)
                xref.TraktTitle = traktShow.Title;

            if (excludeFromWebCache)
                xref.CrossRefSource = (int) CrossRefSource.WebCache;
            else
                xref.CrossRefSource = (int) CrossRefSource.User;

            RepoFactory.CrossRef_AniDB_TraktV2.Save(xref);

            SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);

            logger.Trace("Changed trakt association: {0}", animeID);

            return string.Empty;
        }

        public static void RemoveLinkAniDBTrakt(int animeID, EpisodeType aniEpType, int aniEpNumber, string traktID,
            int seasonNumber, int traktEpNumber)
        {
            CrossRef_AniDB_TraktV2 xref = RepoFactory.CrossRef_AniDB_TraktV2.GetByTraktID(traktID, seasonNumber,
                traktEpNumber, animeID,
                (int) aniEpType,
                aniEpNumber);
            if (xref == null) return;

            RepoFactory.CrossRef_AniDB_TraktV2.Delete(xref.CrossRef_AniDB_TraktV2ID);

            SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);
        }

        public static void ScanForMatches()
        {
            if (!ServerSettings.Instance.TraktTv.Enabled) return;

            Analytics.PostEvent("TraktTV", nameof(ScanForMatches));

            IReadOnlyList<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll();

            IReadOnlyList<CrossRef_AniDB_TraktV2> allCrossRefs = RepoFactory.CrossRef_AniDB_TraktV2.GetAll();
            List<int> alreadyLinked = new List<int>();
            foreach (CrossRef_AniDB_TraktV2 xref in allCrossRefs)
            {
                alreadyLinked.Add(xref.AnimeID);
            }

            foreach (SVR_AnimeSeries ser in allSeries)
            {
                if (alreadyLinked.Contains(ser.AniDB_ID)) continue;

                SVR_AniDB_Anime anime = ser.GetAnime();

                if (anime != null)
                    logger.Trace("Found anime without Trakt association: " + anime.MainTitle);

                if (anime.IsTraktLinkDisabled()) continue;

                CommandRequest_TraktSearchAnime cmd = new CommandRequest_TraktSearchAnime(ser.AniDB_ID, false);
                cmd.Save();
            }
        }

        private static int? GetTraktEpisodeIdV2(SVR_AnimeEpisode ep, ref string traktID, ref int season,
            ref int epNumber)
        {
            AniDB_Episode aniep = ep?.AniDB_Episode;
            if (aniep == null) return null;

            SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(aniep.AnimeID);
            if (anime == null)
                return null;

            return GetTraktEpisodeIdV2(anime, aniep, ref traktID, ref season, ref epNumber);
        }

        private static int? GetTraktEpisodeIdV2(SVR_AniDB_Anime anime, AniDB_Episode ep, ref string traktID,
            ref int season,
            ref int epNumber)
        {
            if (anime == null || ep == null)
                return null;

            TraktSummaryContainer traktSummary = new TraktSummaryContainer();

            traktSummary.Populate(anime.AnimeID);

            return GetTraktEpisodeIdV2(traktSummary, ep, ref traktID, ref season, ref epNumber);
        }

        private static int? GetTraktEpisodeIdV2(TraktSummaryContainer traktSummary,
            AniDB_Episode ep,
            ref string traktID, ref int season, ref int epNumber)
        {
            try
            {
                if (traktSummary == null) return null;
                int? traktEpId = null;

                #region normal episodes

                // now do stuff to improve performance
                if (ep.GetEpisodeTypeEnum() == EpisodeType.Episode)
                {
                    if (traktSummary.CrossRefTraktV2 != null &&
                        traktSummary.CrossRefTraktV2.Count > 0)
                    {
                        // find the xref that is right
                        // relies on the xref's being sorted by season number and then episode number (desc)
                        List<CrossRef_AniDB_TraktV2> traktCrossRef =
                            traktSummary.CrossRefTraktV2.OrderByDescending(a => a.AniDBStartEpisodeNumber).ToList();

                        bool foundStartingPoint = false;
                        CrossRef_AniDB_TraktV2 xrefBase = null;
                        foreach (CrossRef_AniDB_TraktV2 xrefTrakt in traktCrossRef)
                        {
                            if (xrefTrakt.AniDBStartEpisodeType != (int) EpisodeType.Episode) continue;
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

                            if (dictTraktSeasons != null && dictTraktSeasons.ContainsKey(xrefBase.TraktSeasonNumber))
                            {
                                int episodeNumber = dictTraktSeasons[xrefBase.TraktSeasonNumber] +
                                                    (ep.EpisodeNumber + xrefBase.TraktStartEpisodeNumber - 2) -
                                                    (xrefBase.AniDBStartEpisodeNumber - 1);
                                if (dictTraktEpisodes.ContainsKey(episodeNumber))
                                {
                                    Trakt_Episode traktep = dictTraktEpisodes[episodeNumber];
                                    traktID = xrefBase.TraktID;
                                    season = traktep.Season;
                                    epNumber = traktep.EpisodeNumber;
                                    traktEpId = traktep.TraktID;
                                }
                            }
                        }
                    }
                }

                #endregion

                #region special episodes

                if (ep.GetEpisodeTypeEnum() == EpisodeType.Special)
                {
                    // find the xref that is right
                    // relies on the xref's being sorted by season number and then episode number (desc)
                    List<CrossRef_AniDB_TraktV2> traktCrossRef =
                        traktSummary.CrossRefTraktV2?.OrderByDescending(a => a.AniDBStartEpisodeNumber).ToList();

                    if (traktCrossRef == null) return null;
                    bool foundStartingPoint = false;
                    CrossRef_AniDB_TraktV2 xrefBase = null;
                    foreach (CrossRef_AniDB_TraktV2 xrefTrakt in traktCrossRef)
                    {
                        if (xrefTrakt.AniDBStartEpisodeType != (int) EpisodeType.Special) continue;
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

                        if (dictTraktSeasons != null && dictTraktSeasons.ContainsKey(xrefBase.TraktSeasonNumber))
                        {
                            int episodeNumber = dictTraktSeasons[xrefBase.TraktSeasonNumber] +
                                                (ep.EpisodeNumber + xrefBase.TraktStartEpisodeNumber - 2) -
                                                (xrefBase.AniDBStartEpisodeNumber - 1);
                            if (dictTraktEpisodes != null && dictTraktEpisodes.ContainsKey(episodeNumber))
                            {
                                Trakt_Episode traktep = dictTraktEpisodes[episodeNumber];
                                traktID = xrefBase.TraktID;
                                season = traktep.Season;
                                epNumber = traktep.EpisodeNumber;
                                traktEpId = traktep.TraktID;
                            }
                        }
                    }
                }

                #endregion

                return traktEpId;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        #endregion

        /// <summary>
        /// Updates the followung
        /// 1. Series Info
        /// 2. Episode Info
        /// 3. Episode Images
        /// 4. Fanart, Poster Images
        /// </summary>
        /// <param name="traktID"></param>
        public static void UpdateAllInfo(string traktID)
        {
            GetShowInfoV2(traktID);
        }

        #region Send Data to Trakt

        public static CL_Response<bool> PostCommentShow(string traktSlug, string commentText, bool isSpoiler)
        {
            CL_Response<bool> ret = new CL_Response<bool>();
            try
            {
                if (!ServerSettings.Instance.TraktTv.Enabled)
                {
                    ret.ErrorMessage = "Trakt has not been enabled";
                    ret.Result = false;
                    return ret;
                }
                if (string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                {
                    ret.ErrorMessage = "Trakt has not been authorized";
                    ret.Result = false;
                    return ret;
                }

                if (string.IsNullOrEmpty(commentText))
                {
                    ret.ErrorMessage = "Please enter text for your comment";
                    ret.Result = false;
                    return ret;
                }

                TraktV2CommentShowPost comment = new TraktV2CommentShowPost();
                comment.Init(commentText, isSpoiler, traktSlug);

                string json = JSONHelper.Serialize(comment);


                string retData = string.Empty;
                int response = SendData(TraktURIs.PostComment, json, "POST", BuildRequestHeaders(), ref retData);
                if (response == TraktStatusCodes.Success || response == TraktStatusCodes.Success_Post ||
                    response == TraktStatusCodes.Success_Delete)
                {
                    ret.ErrorMessage = "Success";
                    ret.Result = true;
                    return ret;
                }
                ret.ErrorMessage = $"{response} Error - {retData}";
                ret.Result = false;
                return ret;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TraktTVHelper.PostCommentShow: " + ex);
                ret.ErrorMessage = ex.Message;
                ret.Result = false;
                return ret;
            }
        }

        private static DateTime GetEpisodeDateForSync(SVR_AnimeEpisode ep, TraktSyncType syncType)
        {
            DateTime epDate;

            if (syncType == TraktSyncType.CollectionAdd || syncType == TraktSyncType.CollectionRemove)
            {
                epDate = DateTime.Now; // not relevant for a remove
                if (syncType == TraktSyncType.CollectionAdd)
                {
                    // get the the first file that was added to this episode
                    DateTime? thisDate = null;
                    foreach (SVR_VideoLocal vid in ep.GetVideoLocals())
                    {
                        if (!thisDate.HasValue)
                            thisDate = vid.DateTimeCreated;

                        if (vid.DateTimeCreated < thisDate)
                            thisDate = vid.DateTimeCreated;
                    }
                    if (thisDate.HasValue)
                        epDate = thisDate.Value;
                }
            }
            else
            {
                epDate = DateTime.Now; // not relevant for a remove
                if (syncType == TraktSyncType.HistoryAdd)
                {
                    // get the latest user record and find the latest date this episode was watched
                    DateTime? thisDate = null;
                    List<SVR_JMMUser> traktUsers = RepoFactory.JMMUser.GetTraktUsers();
                    if (traktUsers.Count > 0)
                    {
                        foreach (SVR_JMMUser juser in traktUsers)
                        {
                            var userRecord = ep.GetUserRecord(juser.JMMUserID);
                            if (userRecord != null)
                            {
                                if (!thisDate.HasValue && userRecord.WatchedDate.HasValue)
                                    thisDate = userRecord.WatchedDate;
                                if (userRecord.WatchedDate.HasValue && thisDate.HasValue &&
                                    userRecord.WatchedDate > thisDate)
                                    thisDate = userRecord.WatchedDate;
                            }
                        }
                        if (thisDate.HasValue)
                            epDate = thisDate.Value;
                    }
                }
            }

            return epDate;
        }

        public static void SyncEpisodeToTrakt(SVR_AnimeEpisode ep, TraktSyncType syncType)
        {
            try
            {
                if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                    return;

                string traktShowID = string.Empty;
                int season = -1;
                int epNumber = -1;

                GetTraktEpisodeIdV2(ep, ref traktShowID, ref season, ref epNumber);
                if (string.IsNullOrEmpty(traktShowID) || season < 0 || epNumber < 0) return;

                DateTime epDate = GetEpisodeDateForSync(ep, syncType);

                //SyncEpisodeToTrakt(syncType, traktEpisodeId.Value, secondaryAction);
                SyncEpisodeToTrakt(syncType, traktShowID, season, epNumber, epDate);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TraktTVHelper.SyncEpisodeToTrakt: " + ex);
            }
        }

        public static void SyncEpisodeToTrakt(TraktSyncType syncType, string slug, int season, int epNumber,
            DateTime epDate)
        {
            try
            {
                if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                    return;

                string json;
                if (syncType == TraktSyncType.CollectionAdd || syncType == TraktSyncType.CollectionRemove)
                {
                    TraktV2SyncCollectionEpisodesByNumber sync = new TraktV2SyncCollectionEpisodesByNumber(slug, season,
                        epNumber,
                        epDate);
                    json = JSONHelper.Serialize(sync);
                }
                else
                {
                    TraktV2SyncWatchedEpisodesByNumber sync = new TraktV2SyncWatchedEpisodesByNumber(slug, season,
                        epNumber, epDate);
                    json = JSONHelper.Serialize(sync);
                }


                string url = TraktURIs.SyncCollectionAdd;
                switch (syncType)
                {
                    case TraktSyncType.CollectionAdd:
                        url = TraktURIs.SyncCollectionAdd;
                        break;
                    case TraktSyncType.CollectionRemove:
                        url = TraktURIs.SyncCollectionRemove;
                        break;
                    case TraktSyncType.HistoryAdd:
                        url = TraktURIs.SyncHistoryAdd;
                        break;
                    case TraktSyncType.HistoryRemove:
                        url = TraktURIs.SyncHistoryRemove;
                        break;
                }

                string retData = string.Empty;
                SendData(url, json, "POST", BuildRequestHeaders(), ref retData);
            }
            catch (Exception ex)
            {
                logger.Error($"Error in TraktTVHelper.SyncEpisodeToTrakt: {ex}");
            }
        }

        public static int Scrobble(ScrobblePlayingType scrobbleType, string AnimeEpisodeID,
            ScrobblePlayingStatus scrobbleStatus, float progress)
        {
            try
            {
                if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                    return 401;

                string json = string.Empty;

                string url;
                switch (scrobbleStatus)
                {
                    case ScrobblePlayingStatus.Start:
                        url = TraktURIs.SetScrobbleStart;
                        break;
                    case ScrobblePlayingStatus.Pause:
                        url = TraktURIs.SetScrobblePause;
                        break;
                    case ScrobblePlayingStatus.Stop:
                        url = TraktURIs.SetScrobbleStop;
                        break;
                    default:
                        return 400;
                }

                //1.get traktid and slugid from episode id
                if (!int.TryParse(AnimeEpisodeID, out int aep)) return 400;
                SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(aep);
                string slugID = string.Empty;
                int season = 0;
                int epNumber = 0;
                int? traktID = GetTraktEpisodeIdV2(ep, ref slugID, ref season, ref epNumber);

                //2.generate json
                if (traktID != null && traktID > 0)
                {
                    switch (scrobbleType)
                    {
                        case ScrobblePlayingType.episode:
                            TraktV2ScrobbleEpisode showE = new TraktV2ScrobbleEpisode();
                            showE.Init(progress, traktID, slugID, season, epNumber);
                            json = JSONHelper.Serialize(showE);
                            break;

                        //do we have any movies that work?
                        case ScrobblePlayingType.movie:
                            TraktV2ScrobbleMovie showM = new TraktV2ScrobbleMovie();
                            json = JSONHelper.Serialize(showM);
                            showM.Init(progress, slugID, traktID.ToString());
                            break;
                    }
                    //3. send Json
                    string retData = string.Empty;
                    SendData(url, json, "POST", BuildRequestHeaders(), ref retData);
                }
                else
                {
                    //3. nothing to send log error
                    logger.Warn("TraktTVHelper.Scrobble: No TraktID found for: " + "AnimeEpisodeID: " + aep +
                                " AnimeRomajiName: " + ep.Title);
                    return 404;
                }
                return 200;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TraktTVHelper.Scrobble: " + ex);
                return 500;
            }
        }

        #endregion

        #region Get Data From Trakt

        public static List<TraktV2SearchShowResult> SearchShowV2(string criteria)
        {
            List<TraktV2SearchShowResult> results = new List<TraktV2SearchShowResult>();

            if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                return results;

            try
            {
                // replace spaces with a + symbo
                //criteria = criteria.Replace(' ', '+');

                // Search for a series
                string url = string.Format(TraktURIs.Search, criteria, TraktSearchType.show);
                logger.Trace($"Search Trakt Show: {url}");

                // Search for a series
                string json = GetFromTrakt(url);

                if (string.IsNullOrEmpty(json)) return new List<TraktV2SearchShowResult>();

                var result = json.FromJSONArray<TraktV2SearchShowResult>();
                if (result == null) return null;

                return new List<TraktV2SearchShowResult>(result);

                // save this data for later use
                //foreach (TraktTVShow tvshow in results)
                //    SaveExtendedShowInfo(tvshow);
            }
            catch (Exception ex)
            {
                logger.Error($"Error in Trakt SearchSeries: {ex}");
            }

            return null;
        }

        public static List<TraktV2SearchTvDBIDShowResult> SearchShowByIDV2(string idType, string id)
        {
            List<TraktV2SearchTvDBIDShowResult> results = new List<TraktV2SearchTvDBIDShowResult>();

            if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                return results;

            try
            {
                // Search for a series
                string url = string.Format(TraktURIs.SearchByID, idType, id);
                logger.Trace("Search Trakt Show: {0}", url);

                // Search for a series
                string json = GetFromTrakt(url);

                if (string.IsNullOrEmpty(json)) return new List<TraktV2SearchTvDBIDShowResult>();

                //var result2 = json.FromJSONArray<Class1>();
                var result = json.FromJSONArray<TraktV2SearchTvDBIDShowResult>();
                if (result == null) return null;

                return new List<TraktV2SearchTvDBIDShowResult>(result);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in SearchSeries: " + ex);
            }

            return null;
        }


        public static TraktV2ShowExtended GetShowInfoV2(string traktID)
        {
            int traktCode = TraktStatusCodes.Success;
            return GetShowInfoV2(traktID, ref traktCode);
        }

        public static TraktV2ShowExtended GetShowInfoV2(string traktID, ref int traktCode)
        {
            TraktV2ShowExtended resultShow;

            if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                return null;

            try
            {
                string url = string.Format(TraktURIs.ShowSummary, traktID);
                logger.Trace("GetShowInfo: {0}", url);

                // Search for a series
                string json = GetFromTrakt(url, ref traktCode);

                if (string.IsNullOrEmpty(json)) return null;

                resultShow = json.FromJSON<TraktV2ShowExtended>();
                if (resultShow == null) return null;

                // if we got the show info, also download the seaon info
                url = string.Format(TraktURIs.ShowSeasons, traktID);
                logger.Trace("GetSeasonInfo: {0}", url);
                json = GetFromTrakt(url);

                List<TraktV2Season> seasons = new List<TraktV2Season>();
                if (!string.IsNullOrEmpty(json))
                {
                    var resultSeasons = json.FromJSONArray<TraktV2Season>();
                    foreach (TraktV2Season season in resultSeasons)
                        seasons.Add(season);
                }

                // save this data to the DB for use later
                SaveExtendedShowInfoV2(resultShow, seasons);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TraktTVHelper.GetShowInfo: " + ex);
                return null;
            }

            return resultShow;
        }

        public static void SaveExtendedShowInfoV2(TraktV2ShowExtended tvshow, List<TraktV2Season> seasons)
        {
            try
            {
                // save this data to the DB for use later
                Trakt_Show show = RepoFactory.Trakt_Show.GetByTraktSlug(tvshow.ids.slug) ?? new Trakt_Show();

                show.Populate(tvshow);
                RepoFactory.Trakt_Show.Save(show);

                // save the seasons

                // delete episodes if they no longer exist on Trakt
                if (seasons.Count > 0)
                {
                    foreach (Trakt_Episode epTemp in RepoFactory.Trakt_Episode.GetByShowID(show.Trakt_ShowID))
                    {
                        TraktV2Episode ep = null;
                        TraktV2Season sea = seasons.FirstOrDefault(x => x.number == epTemp.Season);
                        if (sea != null)
                            ep = sea.episodes.FirstOrDefault(x => x.number == epTemp.EpisodeNumber);

                        // if the episode is null, it means it doesn't exist on Trakt, so we should delete it
                        if (ep == null)
                            RepoFactory.Trakt_Episode.Delete(epTemp.Trakt_EpisodeID);
                    }
                }

                foreach (TraktV2Season sea in seasons)
                {
                    Trakt_Season season = RepoFactory.Trakt_Season.GetByShowIDAndSeason(show.Trakt_ShowID, sea.number) ??
                                          new Trakt_Season();

                    season.Season = sea.number;
                    season.URL = string.Format(TraktURIs.WebsiteSeason, show.TraktID, sea.number);
                    season.Trakt_ShowID = show.Trakt_ShowID;
                    RepoFactory.Trakt_Season.Save(season);

                    if (sea.episodes != null)
                    {
                        foreach (TraktV2Episode ep in sea.episodes)
                        {
                            Trakt_Episode episode = RepoFactory.Trakt_Episode.GetByShowIDSeasonAndEpisode(
                                                        show.Trakt_ShowID, ep.season,
                                                        ep.number) ?? new Trakt_Episode();

                            episode.TraktID = ep.ids.TraktID;
                            episode.EpisodeNumber = ep.number;
                            episode.Overview = string.Empty;
                            // this is now part of a separate API call for V2, we get this info from TvDB anyway
                            episode.Season = ep.season;
                            episode.Title = ep.title;
                            episode.URL = string.Format(TraktURIs.WebsiteEpisode, show.TraktID, ep.season, ep.number);
                            episode.Trakt_ShowID = show.Trakt_ShowID;
                            RepoFactory.Trakt_Episode.Save(episode);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TraktTVHelper.SaveExtendedShowInfo: " + ex);
            }
        }

        public static List<TraktV2Comment> GetShowCommentsV2(int animeID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetShowCommentsV2(session, animeID);
            }
        }

        public static List<TraktV2Comment> GetShowCommentsV2(ISession session, int animeID)
        {
            List<TraktV2Comment> ret = new List<TraktV2Comment>();
            try
            {
                if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                    return ret;

                List<CrossRef_AniDB_TraktV2> traktXRefs =
                    RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(session, animeID);
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
                        logger.Trace("GetShowComments: {0}", url);

                        string json = GetFromTrakt(url);

                        if (string.IsNullOrEmpty(json))
                            return null;

                        var resultComments = json.FromJSONArray<TraktV2Comment>();
                        if (resultComments != null)
                        {
                            List<TraktV2Comment> thisComments = new List<TraktV2Comment>(resultComments);
                            ret.AddRange(thisComments);

                            if (thisComments.Count != TraktConstants.PaginationLimit)
                            {
                                morePages = false;
                            }
                        }
                        else
                            morePages = false;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in TraktTVHelper.GetShowComments: {ex}");
            }

            return ret;
        }

        public static List<TraktV2ShowWatchedResult> GetWatchedShows(ref int traktCode)
        {
            if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                return new List<TraktV2ShowWatchedResult>();

            try
            {
                // Search for a series
                string url = string.Format(TraktURIs.GetWatchedShows);
                logger.Trace($"Get All Watched Shows and Episodes: {url}");

                // Search for a series
                string json = GetFromTrakt(url, ref traktCode);

                if (string.IsNullOrEmpty(json)) return new List<TraktV2ShowWatchedResult>();

                var result = json.FromJSONArray<TraktV2ShowWatchedResult>();
                if (result == null) return new List<TraktV2ShowWatchedResult>();

                return new List<TraktV2ShowWatchedResult>(result);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in SearchSeries: " + ex);
            }

            return new List<TraktV2ShowWatchedResult>();
        }

        public static List<TraktV2ShowCollectedResult> GetCollectedShows(ref int traktCode)
        {
            if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                return new List<TraktV2ShowCollectedResult>();

            try
            {
                // Search for a series
                string url = string.Format(TraktURIs.GetCollectedShows);
                logger.Trace("Get All Collected Shows and Episodes: {0}", url);

                // Search for a series
                string json = GetFromTrakt(url, ref traktCode);

                if (string.IsNullOrEmpty(json)) return new List<TraktV2ShowCollectedResult>();


                var result = json.FromJSONArray<TraktV2ShowCollectedResult>();
                if (result == null) return new List<TraktV2ShowCollectedResult>();

                return new List<TraktV2ShowCollectedResult>(result);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in SearchSeries: " + ex);
            }

            return new List<TraktV2ShowCollectedResult>();
        }

        #endregion

        public static void UpdateAllInfo()
        {
            if (!ServerSettings.Instance.TraktTv.Enabled) return;

            IReadOnlyList<CrossRef_AniDB_TraktV2> allCrossRefs = RepoFactory.CrossRef_AniDB_TraktV2.GetAll();
            foreach (CrossRef_AniDB_TraktV2 xref in allCrossRefs)
            {
                CommandRequest_TraktUpdateInfo cmd = new CommandRequest_TraktUpdateInfo(xref.TraktID);
                cmd.Save();
            }
        }

        public static void SyncCollectionToTrakt_Series(SVR_AnimeSeries series)
        {
            try
            {
                // check that we have at least one user nominated for Trakt
                List<SVR_JMMUser> traktUsers = RepoFactory.JMMUser.GetTraktUsers();
                if (traktUsers.Count == 0) return;

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(series.AniDB_ID);
                if (anime == null) return;

                TraktSummaryContainer traktSummary = new TraktSummaryContainer();
                traktSummary.Populate(series.AniDB_ID);
                if (traktSummary.CrossRefTraktV2 == null || traktSummary.CrossRefTraktV2.Count == 0) return;

                // now get the full users collection from Trakt
                List<TraktV2ShowCollectedResult> collected = new List<TraktV2ShowCollectedResult>();
                List<TraktV2ShowWatchedResult> watched = new List<TraktV2ShowWatchedResult>();

                if (!GetTraktCollectionInfo(ref collected, ref watched)) return;

                foreach (SVR_AnimeEpisode ep in series.GetAnimeEpisodes())
                {
                    if (ep.EpisodeTypeEnum == EpisodeType.Episode || ep.EpisodeTypeEnum == EpisodeType.Special)
                    {
                        ReconSyncTraktEpisode(series, ep, traktUsers, collected, watched, true);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TraktTVHelper.SyncCollectionToTrakt_Series: " + ex);
            }
        }

        public static void SyncCollectionToTrakt()
        {
            try
            {
                if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken)) return;

                // check that we have at least one user nominated for Trakt
                List<SVR_JMMUser> traktUsers = RepoFactory.JMMUser.GetTraktUsers();
                if (traktUsers.Count == 0) return;

                IReadOnlyList<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll();

                // now get the full users collection from Trakt
                List<TraktV2ShowCollectedResult> collected = new List<TraktV2ShowCollectedResult>();
                List<TraktV2ShowWatchedResult> watched = new List<TraktV2ShowWatchedResult>();

                if (!GetTraktCollectionInfo(ref collected, ref watched)) return;

                TraktV2SyncCollectionEpisodesByNumber syncCollectionAdd = new TraktV2SyncCollectionEpisodesByNumber();
                TraktV2SyncCollectionEpisodesByNumber syncCollectionRemove =
                    new TraktV2SyncCollectionEpisodesByNumber();
                TraktV2SyncWatchedEpisodesByNumber syncHistoryAdd = new TraktV2SyncWatchedEpisodesByNumber();
                TraktV2SyncWatchedEpisodesByNumber syncHistoryRemove = new TraktV2SyncWatchedEpisodesByNumber();

                #region Local Collection Sync

                ///////////////////////////////////////////////////////////////////////////////////////
                // First take a look at our local collection and update on Trakt
                ///////////////////////////////////////////////////////////////////////////////////////

                int counter = 0;
                foreach (SVR_AnimeSeries series in allSeries)
                {
                    counter++;
                    logger.Trace("Syncing check -  local collection: {0} / {1} - {2}", counter, allSeries.Count,
                        series.GetSeriesName());

                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(series.AniDB_ID);
                    if (anime == null) continue;

                    //if (anime.AnimeID != 3427) continue;

                    TraktSummaryContainer traktSummary = new TraktSummaryContainer();
                    traktSummary.Populate(series.AniDB_ID);
                    if (traktSummary.CrossRefTraktV2 == null || traktSummary.CrossRefTraktV2.Count == 0) continue;

                    // get the current watched records for this series on Trakt

                    foreach (SVR_AnimeEpisode ep in series.GetAnimeEpisodes())
                    {
                        if (ep.EpisodeTypeEnum == EpisodeType.Episode || ep.EpisodeTypeEnum == EpisodeType.Special)
                        {
                            EpisodeSyncDetails epsync = ReconSyncTraktEpisode(series, ep, traktUsers,
                                collected, watched, false);
                            if (epsync != null)
                            {
                                switch (epsync.SyncType)
                                {
                                    case TraktSyncType.CollectionAdd:
                                        syncCollectionAdd.AddEpisode(epsync.Slug, epsync.Season, epsync.EpNumber,
                                            epsync.EpDate);
                                        break;
                                    case TraktSyncType.CollectionRemove:
                                        syncCollectionRemove.AddEpisode(epsync.Slug, epsync.Season, epsync.EpNumber,
                                            epsync.EpDate);
                                        break;
                                    case TraktSyncType.HistoryAdd:
                                        syncHistoryAdd.AddEpisode(epsync.Slug, epsync.Season, epsync.EpNumber,
                                            epsync.EpDate);
                                        break;
                                    case TraktSyncType.HistoryRemove:
                                        syncHistoryRemove.AddEpisode(epsync.Slug, epsync.Season, epsync.EpNumber,
                                            epsync.EpDate);
                                        break;
                                }
                            }
                        }
                    }
                }

                #endregion

                // refresh online info, just in case it was chnaged by the last operations
                if (!GetTraktCollectionInfo(ref collected, ref watched)) return;

                #region Online Collection Sync

                ///////////////////////////////////////////////////////////////////////////////////////
                // Now look at the collection according to Trakt, and remove it if we don't have it locally
                ///////////////////////////////////////////////////////////////////////////////////////


                counter = 0;
                foreach (TraktV2ShowCollectedResult col in collected)
                {
                    counter++;
                    logger.Trace("Syncing check - Online collection: {0} / {1} - {2}", counter, collected.Count,
                        col.show.Title);
                    //continue;

                    // check if we have this series locally
                    List<CrossRef_AniDB_TraktV2> xrefs =
                        RepoFactory.CrossRef_AniDB_TraktV2.GetByTraktID(col.show.ids.slug);

                    if (xrefs.Count > 0)
                    {
                        foreach (CrossRef_AniDB_TraktV2 xref in xrefs)
                        {
                            SVR_AnimeSeries locSeries = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID);
                            if (locSeries == null) continue;

                            TraktSummaryContainer traktSummary = new TraktSummaryContainer();
                            traktSummary.Populate(locSeries.AniDB_ID);
                            if (traktSummary.CrossRefTraktV2 == null || traktSummary.CrossRefTraktV2.Count == 0)
                                continue;

                            // if we have this series locSeries, let's sync the whole series
                            foreach (SVR_AnimeEpisode ep in locSeries.GetAnimeEpisodes())
                            {
                                if (ep.EpisodeTypeEnum == EpisodeType.Episode ||
                                    ep.EpisodeTypeEnum == EpisodeType.Special)
                                {
                                    EpisodeSyncDetails epsync = ReconSyncTraktEpisode(locSeries, ep,
                                        traktUsers, collected, watched,
                                        false);
                                    if (epsync != null)
                                    {
                                        switch (epsync.SyncType)
                                        {
                                            case TraktSyncType.CollectionAdd:
                                                syncCollectionAdd.AddEpisode(epsync.Slug, epsync.Season,
                                                    epsync.EpNumber,
                                                    epsync.EpDate);
                                                break;
                                            case TraktSyncType.CollectionRemove:
                                                syncCollectionRemove.AddEpisode(epsync.Slug, epsync.Season,
                                                    epsync.EpNumber, epsync.EpDate);
                                                break;
                                            case TraktSyncType.HistoryAdd:
                                                syncHistoryAdd.AddEpisode(epsync.Slug, epsync.Season, epsync.EpNumber,
                                                    epsync.EpDate);
                                                break;
                                            case TraktSyncType.HistoryRemove:
                                                syncHistoryRemove.AddEpisode(epsync.Slug, epsync.Season,
                                                    epsync.EpNumber,
                                                    epsync.EpDate);
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                #endregion

                // refresh online info, just in case it was chnaged by the last operations
                if (!GetTraktCollectionInfo(ref collected, ref watched)) return;

                #region Online History (Watched/Unwatched) Sync

                ///////////////////////////////////////////////////////////////////////////////////////
                // Now look at the history according to Trakt, and remove it if we don't have it locally
                ///////////////////////////////////////////////////////////////////////////////////////

                counter = 0;

                foreach (TraktV2ShowWatchedResult wtch in watched)
                {
                    counter++;
                    logger.Trace("Syncing check - Online History: {0} / {1} - {2}", counter, watched.Count,
                        wtch.show.Title);
                    //continue;

                    // check if we have this series locally
                    List<CrossRef_AniDB_TraktV2> xrefs =
                        RepoFactory.CrossRef_AniDB_TraktV2.GetByTraktID(wtch.show.ids.slug);

                    if (xrefs.Count > 0)
                    {
                        foreach (CrossRef_AniDB_TraktV2 xref in xrefs)
                        {
                            SVR_AnimeSeries locSeries = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID);
                            if (locSeries == null) continue;

                            TraktSummaryContainer traktSummary = new TraktSummaryContainer();
                            traktSummary.Populate(locSeries.AniDB_ID);
                            if (traktSummary.CrossRefTraktV2 == null || traktSummary.CrossRefTraktV2.Count == 0)
                                continue;

                            // if we have this series locSeries, let's sync the whole series
                            foreach (SVR_AnimeEpisode ep in locSeries.GetAnimeEpisodes())
                            {
                                if (ep.EpisodeTypeEnum == EpisodeType.Episode ||
                                    ep.EpisodeTypeEnum == EpisodeType.Special)
                                {
                                    EpisodeSyncDetails epsync = ReconSyncTraktEpisode(locSeries, ep,
                                        traktUsers, collected, watched,
                                        false);
                                    if (epsync != null)
                                    {
                                        switch (epsync.SyncType)
                                        {
                                            case TraktSyncType.CollectionAdd:
                                                syncCollectionAdd.AddEpisode(epsync.Slug, epsync.Season,
                                                    epsync.EpNumber,
                                                    epsync.EpDate);
                                                break;
                                            case TraktSyncType.CollectionRemove:
                                                syncCollectionRemove.AddEpisode(epsync.Slug, epsync.Season,
                                                    epsync.EpNumber, epsync.EpDate);
                                                break;
                                            case TraktSyncType.HistoryAdd:
                                                syncHistoryAdd.AddEpisode(epsync.Slug, epsync.Season, epsync.EpNumber,
                                                    epsync.EpDate);
                                                break;
                                            case TraktSyncType.HistoryRemove:
                                                syncHistoryRemove.AddEpisode(epsync.Slug, epsync.Season,
                                                    epsync.EpNumber,
                                                    epsync.EpDate);
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                #endregion

                // send the data to Trakt
                string json;
                string url;
                string retData;

                if (syncCollectionAdd.shows != null && syncCollectionAdd.shows.Count > 0)
                {
                    json = JSONHelper.Serialize(syncCollectionAdd);
                    url = TraktURIs.SyncCollectionAdd;
                    retData = string.Empty;
                    SendData(url, json, "POST", BuildRequestHeaders(), ref retData);
                }

                if (syncCollectionRemove.shows != null && syncCollectionRemove.shows.Count > 0)
                {
                    json = JSONHelper.Serialize(syncCollectionRemove);
                    url = TraktURIs.SyncCollectionRemove;
                    retData = string.Empty;
                    SendData(url, json, "POST", BuildRequestHeaders(), ref retData);
                }

                if (syncHistoryAdd.shows != null && syncHistoryAdd.shows.Count > 0)
                {
                    json = JSONHelper.Serialize(syncHistoryAdd);
                    url = TraktURIs.SyncHistoryAdd;
                    retData = string.Empty;
                    SendData(url, json, "POST", BuildRequestHeaders(), ref retData);
                }

                if (syncHistoryRemove.shows != null && syncHistoryRemove.shows.Count > 0)
                {
                    json = JSONHelper.Serialize(syncHistoryRemove);
                    url = TraktURIs.SyncHistoryRemove;
                    retData = string.Empty;
                    SendData(url, json, "POST", BuildRequestHeaders(), ref retData);
                }


                logger.Trace("Test");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TraktTVHelper.SyncCollectionToTrakt: " + ex);
            }
        }

        public static bool CheckTraktValidity(string slug, bool removeDBEntries)
        {
            try
            {
                // let's check if we can get this show on Trakt
                int traktCode = TraktStatusCodes.Success;
                // get all the shows from the database and make sure they are still valid Trakt Slugs
                Trakt_Show show = RepoFactory.Trakt_Show.GetByTraktSlug(slug);
                if (show == null)
                {
                    logger.Error($"Unable to get Trakt Show for \"{slug}\". Attempting to download info, anyway.");
                    TraktV2ShowExtended tempShow = GetShowInfoV2(slug, ref traktCode);
                    if (tempShow == null || traktCode == TraktStatusCodes.Not_Found)
                    {
                        logger.Error($"\"{slug}\" was not found on Trakt. Not continuing.");
                        return false;
                    }
                    show = RepoFactory.Trakt_Show.GetByTraktSlug(tempShow.ids.slug);
                }

                // note - getting extended show info also updates it as well
                TraktV2ShowExtended showOnline = GetShowInfoV2(show.TraktID, ref traktCode);
                if (showOnline == null && traktCode == TraktStatusCodes.Not_Found)
                {
                    if (removeDBEntries)
                    {
                        logger.Info("TRAKT_CLEANUP: Could not find '{0}' on Trakt so starting removal from database",
                            show.TraktID);
                        RemoveTraktDBEntries(show);
                    }
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TraktTVHelper.CleanupDatabase: " + ex);
                return false;
            }
        }

        public static void RemoveTraktDBEntries(Trakt_Show show)
        {
            // this means Trakt has no record of this slug.
            // 1. Delete any cross ref links
            RepoFactory.CrossRef_AniDB_TraktV2.Delete(RepoFactory.CrossRef_AniDB_TraktV2.GetByTraktID(show.TraktID));

            // 2. Delete default image links

            // 3. Delete episodes
            RepoFactory.Trakt_Episode.Delete(RepoFactory.Trakt_Episode.GetByShowID(show.Trakt_ShowID));

            // 5. Delete seasons
            RepoFactory.Trakt_Season.Delete(RepoFactory.Trakt_Season.GetByShowID(show.Trakt_ShowID));

            // 6. Delete the show
            RepoFactory.Trakt_Show.Delete(show.Trakt_ShowID);
        }

        private static EpisodeSyncDetails ReconSyncTraktEpisode(SVR_AnimeSeries ser, SVR_AnimeEpisode ep,
            List<SVR_JMMUser> traktUsers, List<TraktV2ShowCollectedResult> collected,
            List<TraktV2ShowWatchedResult> watched, bool sendNow)
        {
            try
            {
                // get the Trakt Show ID for this episode
                string traktShowID = string.Empty;
                int season = -1;
                int epNumber = -1;

                GetTraktEpisodeIdV2(ep, ref traktShowID, ref season, ref epNumber);
                if (string.IsNullOrEmpty(traktShowID) || season < 0 || epNumber < 0) return null;

                // get the current collected records for this series on Trakt
                TraktV2CollectedEpisode epTraktCol = null;
                TraktV2ShowCollectedResult col = collected.FirstOrDefault(x => x.show.ids.slug == traktShowID);
                if (col != null)
                {
                    TraktV2CollectedSeason sea = col.seasons.FirstOrDefault(x => x.number == season);
                    if (sea != null)
                    {
                        epTraktCol = sea.episodes.FirstOrDefault(x => x.number == epNumber);
                    }
                }

                bool onlineCollection = epTraktCol != null;

                // get the current watched records for this series on Trakt
                TraktV2WatchedEpisode epTraktWatched = null;
                TraktV2ShowWatchedResult wtc = watched.FirstOrDefault(x => x.show.ids.slug == traktShowID);
                if (wtc != null)
                {
                    TraktV2WatchedSeason sea = wtc.seasons.FirstOrDefault(x => x.number == season);
                    if (sea != null)
                    {
                        epTraktWatched = sea.episodes.FirstOrDefault(x => x.number == epNumber);
                    }
                }

                bool onlineWatched = epTraktWatched != null;

                bool localCollection = false;
                bool localWatched = false;

                // If we have local files check for watched count
                if (ep.GetVideoLocals().Count > 0)
                {
                    localCollection = true;

                    foreach (SVR_JMMUser juser in traktUsers)
                    {
                        // If there's a watch count we mark it as locally watched
                        if (ep.GetUserRecord(juser.JMMUserID)?.WatchedCount > 0)
                            localWatched = true;
                    }
                }

                logger.Trace($"Sync Check Status:  AniDB: {ser.AniDB_ID} - {ep.EpisodeTypeEnum} - {ep.AniDB_EpisodeID} - Collection: {localCollection} - Watched: {localWatched}");
                logger.Trace($"Sync Check Status:  Trakt: {traktShowID} - S:{season} - EP:{epNumber} - Collection: {onlineCollection} - Watched: {onlineWatched}");

                // sync the collection status
                if (localCollection)
                {
                    // is in the local collection, but not Trakt, so let's ADD it
                    if (!onlineCollection)
                    {
                        logger.Trace($"SYNC LOCAL: Adding to Trakt Collection:  Slug: {traktShowID} - S:{season} - EP:{epNumber}");
                        DateTime epDate = GetEpisodeDateForSync(ep, TraktSyncType.CollectionAdd);
                        if (sendNow)
                            SyncEpisodeToTrakt(TraktSyncType.CollectionAdd, traktShowID, season, epNumber, epDate);
                        else
                            return new EpisodeSyncDetails(TraktSyncType.CollectionAdd, traktShowID, season, epNumber,
                                epDate);
                    }
                }
                else
                {
                    // is in the trakt collection, but not local, so let's REMOVE it
                    if (onlineCollection)
                    {
                        logger.Trace($"SYNC LOCAL: Removing from Trakt Collection:  Slug: {traktShowID} - S:{season} - EP:{epNumber}");
                        DateTime epDate = GetEpisodeDateForSync(ep, TraktSyncType.CollectionRemove);
                        if (sendNow)
                            SyncEpisodeToTrakt(TraktSyncType.CollectionRemove, traktShowID, season, epNumber, epDate);
                        else
                            return new EpisodeSyncDetails(TraktSyncType.CollectionRemove, traktShowID, season, epNumber,
                                epDate);
                    }
                }

                // sync the watched status
                if (localWatched)
                {
                    // is watched locally, but not Trakt, so let's ADD it
                    if (!onlineWatched)
                    {
                        logger.Trace($"SYNC LOCAL: Adding to Trakt History:  Slug: {traktShowID} - S:{season} - EP:{epNumber}");
                        DateTime epDate = GetEpisodeDateForSync(ep, TraktSyncType.HistoryAdd);
                        if (sendNow)
                            SyncEpisodeToTrakt(TraktSyncType.HistoryAdd, traktShowID, season, epNumber, epDate);
                        else
                            return new EpisodeSyncDetails(TraktSyncType.HistoryAdd, traktShowID, season, epNumber,
                                epDate);
                    }
                }
                else
                {
                    // is watched on trakt, but not locally, so let's REMOVE it
                    if (onlineWatched)
                    {
                        logger.Trace($"SYNC LOCAL: Removing from Trakt History:  Slug: {traktShowID} - S:{season} - EP:{epNumber}");
                        DateTime epDate = GetEpisodeDateForSync(ep, TraktSyncType.HistoryRemove);
                        if (sendNow)
                            SyncEpisodeToTrakt(TraktSyncType.HistoryRemove, traktShowID, season, epNumber, epDate);
                        else
                            return new EpisodeSyncDetails(TraktSyncType.HistoryRemove, traktShowID, season, epNumber,
                                epDate);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.Error($"Error in TraktTVHelper.SyncTraktEpisode: {ex}");
                return null;
            }
        }

        private static bool GetTraktCollectionInfo(ref List<TraktV2ShowCollectedResult> collected,
            ref List<TraktV2ShowWatchedResult> watched)
        {
            try
            {
                if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                    return false;

                // check that we have at least one user nominated for Trakt
                List<SVR_JMMUser> traktUsers = RepoFactory.JMMUser.GetTraktUsers();
                if (traktUsers.Count == 0) return false;

                int traktCode = TraktStatusCodes.Success;

                // now get the full users collection from Trakt
                collected = GetCollectedShows(ref traktCode);
                if (traktCode != TraktStatusCodes.Success)
                {
                    logger.Error("Could not get users collection: {0}", traktCode);
                    return false;
                }

                // now get all the shows / episodes the user has watched
                watched = GetWatchedShows(ref traktCode);
                if (traktCode != TraktStatusCodes.Success)
                {
                    logger.Error("Could not get users watched history: {0}", traktCode);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TraktTVHelper.GetTraktCollectionInfo: " + ex);
                return false;
            }
        }
    }

    public class EpisodeSyncDetails
    {
        //TraktSyncType syncType, string slug, int season, int epNumber, DateTime epDate
        public TraktSyncType SyncType { get; set; }

        public string Slug { get; set; }

        public int Season { get; set; }

        public int EpNumber { get; set; }

        public DateTime EpDate { get; set; }

        public EpisodeSyncDetails()
        {
        }

        public EpisodeSyncDetails(TraktSyncType syncType, string slug, int season, int epNumber, DateTime epDate)
        {
            SyncType = syncType;
            Slug = slug;
            Season = season;
            EpNumber = epNumber;
            EpDate = epDate;
        }
    }
}
