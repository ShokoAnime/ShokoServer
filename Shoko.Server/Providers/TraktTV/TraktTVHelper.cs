using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NHibernate;
using Sentry;
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

namespace Shoko.Server.Providers.TraktTV;

public class TraktTVHelper
{
    private readonly ILogger<TraktTVHelper> _logger;
    private readonly ICommandRequestFactory _commandFactory;
    private readonly ISettingsProvider _settingsProvider;

    public TraktTVHelper(ILogger<TraktTVHelper> logger, ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider)
    {
        _logger = logger;
        _commandFactory = commandFactory;
        _settingsProvider = settingsProvider;
    }

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

    private int SendData(string uri, string json, string verb, Dictionary<string, string> headers,
        ref string webResponse)
    {
        var ret = 400;

        try
        {
            var data = new UTF8Encoding().GetBytes(json);
            _logger.LogTrace("Trakt SEND Data\\nVerb: {Verb}\\nuri: {Uri}\\njson: {Json}", verb, uri, json);

            var request = (HttpWebRequest)WebRequest.Create(uri);
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
            var postStream = request.GetRequestStream();
            postStream.Write(data, 0, data.Length);

            // get the response
            var response = (HttpWebResponse)request.GetResponse();

            var responseStream = response.GetResponseStream();
            if (responseStream == null)
            {
                return ret;
            }

            var reader = new StreamReader(responseStream);
            var strResponse = reader.ReadToEnd();

            var statusCode = (int)response.StatusCode;

            // cleanup
            postStream.Close();
            responseStream.Close();
            reader.Close();
            response.Close();

            webResponse = strResponse;
            _logger.LogTrace("Trakt SEND Data - Response\nStatus Code: {StatusCode}\nResponse: {Response}", statusCode,
                strResponse);

            return statusCode;
        }
        catch (WebException webEx)
        {
            if (webEx.Status == WebExceptionStatus.ProtocolError)
            {
                if (webEx.Response is HttpWebResponse response)
                    if (!response.ResponseUri.AbsoluteUri.Contains("device/token") && response.StatusCode == HttpStatusCode.BadRequest) {
                        {
                            _logger.LogError(webEx, "Error in SendData: {StatusCode}", (int)response.StatusCode);
                            ret = (int)response.StatusCode;
                        }
                        try
                        {
                            var responseStream2 = response.GetResponseStream();
                            if (responseStream2 == null)
                            {
                                return ret;
                            }

                            var reader2 = new StreamReader(responseStream2);
                            webResponse = reader2.ReadToEnd();
                            _logger.LogError("Error in SendData: {Response}", webResponse);
                        }
                        catch
                        {
                            // ignore
                        }
                    }
            }
            if (webEx.Response != null && !webEx.Response.ResponseUri.AbsoluteUri.Contains("device/token"))
            {
                _logger.LogError(webEx, "{Ex}", webEx.ToString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SendData");
        }

        return ret;
    }

    private string GetFromTrakt(string uri)
    {
        var retCode = 400;
        return GetFromTrakt(uri, ref retCode);
    }

    private string GetFromTrakt(string uri, ref int traktCode)
    {
        var request = (HttpWebRequest)WebRequest.Create(uri);

        _logger.LogTrace("Trakt GET Data\\nuri: {Uri}", uri);

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
            var httpResponse = (HttpWebResponse)response;
            traktCode = (int)httpResponse.StatusCode;
            var stream = response.GetResponseStream();
            if (stream == null)
            {
                return null;
            }

            var reader = new StreamReader(stream);
            var strResponse = reader.ReadToEnd();

            stream.Close();
            reader.Close();
            response.Close();

            // log the response unless it is Full Collection or Full Watched as this data is way too big
            if (!uri.Equals(TraktURIs.GetWatchedShows, StringComparison.InvariantCultureIgnoreCase) &&
                !uri.Equals(TraktURIs.GetCollectedShows, StringComparison.InvariantCultureIgnoreCase))
            {
                _logger.LogTrace("Trakt GET Data - Response\\nResponse: {Response}", strResponse);
            }

            return strResponse;
        }
        catch (WebException e)
        {
            _logger.LogError(e, "Error in GetFromTrakt");

            var httpResponse = (HttpWebResponse)e.Response;
            traktCode = (int?)httpResponse?.StatusCode ?? 0;

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetFromTrakt");
            return null;
        }
    }

    private Dictionary<string, string> BuildRequestHeaders()
    {
        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {_settingsProvider.GetSettings().TraktTv.AuthToken}" },
            { "trakt-api-key", TraktConstants.ClientID },
            { "trakt-api-version", "2" }
        };

        return headers;
    }

    #endregion

    #region Authorization

    public void RefreshAuthToken()
    {
        var settings = _settingsProvider.GetSettings();
        try
        {
            if (!settings.TraktTv.Enabled ||
                string.IsNullOrEmpty(settings.TraktTv.AuthToken) ||
                string.IsNullOrEmpty(settings.TraktTv.RefreshToken))
            {
                settings.TraktTv.AuthToken = string.Empty;
                settings.TraktTv.RefreshToken = string.Empty;
                settings.TraktTv.TokenExpirationDate = string.Empty;

                return;
            }

            var token = new TraktV2RefreshToken { refresh_token = settings.TraktTv.RefreshToken };
            var json = JSONHelper.Serialize(token);
            var headers = new Dictionary<string, string>();

            var retData = string.Empty;
            TraktTVRateLimiter.Instance.EnsureRate();
            var response = SendData(TraktURIs.Oauth, json, "POST", headers, ref retData);
            if (response is TraktStatusCodes.Success or TraktStatusCodes.Success_Post)
            {
                var loginResponse = retData.FromJSON<TraktAuthToken>();

                // save the token to the config file to use for subsequent API calls
                settings.TraktTv.AuthToken = loginResponse.AccessToken;
                settings.TraktTv.RefreshToken = loginResponse.RefreshToken;

                long.TryParse(loginResponse.CreatedAt, out var createdAt);
                long.TryParse(loginResponse.ExpiresIn, out var validity);
                var expireDate = createdAt + validity;

                settings.TraktTv.TokenExpirationDate = expireDate.ToString();

                return;
            }

            settings.TraktTv.AuthToken = string.Empty;
            settings.TraktTv.RefreshToken = string.Empty;
            settings.TraktTv.TokenExpirationDate = string.Empty;
        }
        catch (Exception ex)
        {
            settings.TraktTv.AuthToken = string.Empty;
            settings.TraktTv.RefreshToken = string.Empty;
            settings.TraktTv.TokenExpirationDate = string.Empty;

            _logger.LogError(ex, "Error in TraktTVHelper.RefreshAuthToken");
        }
        finally
        {
            Utils.SettingsProvider.SaveSettings();
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

    public TraktAuthDeviceCodeToken GetTraktDeviceCode()
    {
        try
        {
            var obj = new TraktAuthDeviceCode();
            var json = JSONHelper.Serialize(obj);
            var headers = new Dictionary<string, string>();

            var retData = string.Empty;
            TraktTVRateLimiter.Instance.EnsureRate();
            var response = SendData(TraktURIs.OAuthDeviceCode, json, "POST", headers, ref retData);
            // We need to catch HTTP "400" here, as it's not "bad request" but "awaiting authorization" from the API definition
            if (response != TraktStatusCodes.Success && response != TraktStatusCodes.Success_Post && response != TraktStatusCodes.Awaiting_Auth)
            {
                throw new Exception($"Error returned from Trakt: {response}");
            }

            var deviceCode = retData.FromJSON<TraktAuthDeviceCodeToken>();

            Task.Run(() => { TraktAuthPollDeviceToken(deviceCode); });

            return deviceCode;
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Pending"))
            {
                // Signaling the user that auth is still pending
                _logger.LogInformation(ex, "Authorization for Shoko pending, please enter the code displayed by clicking the link");
            }
            _logger.LogError(ex, "Error in TraktTVHelper.GetTraktDeviceCode");
                throw;
        }
    }

    private void TraktAuthPollDeviceToken(TraktAuthDeviceCodeToken deviceCode)
    {
        if (deviceCode == null)
        {
            return;
        }

        var task = Task.Run(() => { TraktAutoDeviceTokenWorker(deviceCode); });
        if (!task.Wait(TimeSpan.FromSeconds(deviceCode.ExpiresIn)))
        {
            _logger.LogError("Error in TraktTVHelper.TraktAuthPollDeviceToken: Timed out");
        }
    }

    private void TraktAutoDeviceTokenWorker(TraktAuthDeviceCodeToken deviceCode)
    {
        try
        {
            var pollInterval = TimeSpan.FromSeconds(deviceCode.Interval);
            var obj = new TraktAuthDeviceCodePoll { DeviceCode = deviceCode.DeviceCode };
            var json = JSONHelper.Serialize(obj);
            var headers = new Dictionary<string, string>();
            while (true)
            {
                Thread.Sleep(pollInterval);

                headers.Clear();

                var retData = string.Empty;
                TraktTVRateLimiter.Instance.EnsureRate();
                var response = SendData(TraktURIs.OAuthDeviceToken, json, "POST", headers, ref retData);
                if (response == TraktStatusCodes.Success)
                {
                    var settings = _settingsProvider.GetSettings();
                    var tokenResponse = retData.FromJSON<TraktAuthToken>();
                    settings.TraktTv.AuthToken = tokenResponse.AccessToken;
                    settings.TraktTv.RefreshToken = tokenResponse.RefreshToken;

                    long.TryParse(tokenResponse.CreatedAt, out var createdAt);
                    long.TryParse(tokenResponse.ExpiresIn, out var validity);
                    var expireDate = createdAt + validity;

                    settings.TraktTv.TokenExpirationDate = expireDate.ToString();
                    _settingsProvider.SaveSettings();
                    break;
                }

                if (response == TraktStatusCodes.Rate_Limit_Exceeded)
                {
                    //Temporarily increase poll interval
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }

                if (response == TraktStatusCodes.Awaiting_Auth)
                {
                    // Signaling the user that auth is still pending
                    _logger.LogInformation (response, "Authorization for Shoko pending, please enter the code displayed by clicking the link");
                }

                if (response != TraktStatusCodes.Awaiting_Auth && response != TraktStatusCodes.Rate_Limit_Exceeded)
                {
                    throw new Exception($"Error returned from Trakt: {response}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.TraktAuthDeviceCodeToken");
            throw;
        }
    }

    #endregion

    #region Linking

    public string LinkAniDBTrakt(int animeID, EpisodeType aniEpType, int aniEpNumber, string traktID,
        int seasonNumber, int traktEpNumber, bool excludeFromWebCache)
    {
        using var session = DatabaseFactory.SessionFactory.OpenSession();
        return LinkAniDBTrakt(session, animeID, aniEpType, aniEpNumber, traktID, seasonNumber, traktEpNumber,
            excludeFromWebCache);
    }

    public string LinkAniDBTrakt(ISession session, int animeID, EpisodeType aniEpType, int aniEpNumber,
        string traktID, int seasonNumber, int traktEpNumber, bool excludeFromWebCache)
    {
        var xrefTemps = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeIDEpTypeEpNumber(
            session, animeID,
            (int)aniEpType,
            aniEpNumber);
        if (xrefTemps is { Count: > 0 })
        {
            foreach (var xrefTemp in xrefTemps)
            {
                // delete the existing one if we are updating
                RemoveLinkAniDBTrakt(xrefTemp.AnimeID, (EpisodeType)xrefTemp.AniDBStartEpisodeType,
                    xrefTemp.AniDBStartEpisodeNumber,
                    xrefTemp.TraktID, xrefTemp.TraktSeasonNumber, xrefTemp.TraktStartEpisodeNumber);
            }
        }

        // check if we have this information locally
        // if not download it now
        var traktShow = RepoFactory.Trakt_Show.GetByTraktSlug(traktID);
        if (traktShow == null)
        {
            // we download the series info here just so that we have the basic info in the
            // database before the queued task runs later
            GetShowInfoV2(traktID);
            traktShow = RepoFactory.Trakt_Show.GetByTraktSlug(traktID);
        }

        // download and update series info, episode info and episode images

        var xref = RepoFactory.CrossRef_AniDB_TraktV2.GetByTraktID(session, traktID,
            seasonNumber, traktEpNumber,
            animeID,
            (int)aniEpType, aniEpNumber) ?? new CrossRef_AniDB_TraktV2();

        xref.AnimeID = animeID;
        xref.AniDBStartEpisodeType = (int)aniEpType;
        xref.AniDBStartEpisodeNumber = aniEpNumber;

        xref.TraktID = traktID;
        xref.TraktSeasonNumber = seasonNumber;
        xref.TraktStartEpisodeNumber = traktEpNumber;
        if (traktShow != null)
        {
            xref.TraktTitle = traktShow.Title;
        }

        if (excludeFromWebCache)
        {
            xref.CrossRefSource = (int)CrossRefSource.WebCache;
        }
        else
        {
            xref.CrossRefSource = (int)CrossRefSource.User;
        }

        RepoFactory.CrossRef_AniDB_TraktV2.Save(xref);

        SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);

        _logger.LogTrace("Changed trakt association: {AnimeID}", animeID);

        return string.Empty;
    }

    public void RemoveLinkAniDBTrakt(int animeID, EpisodeType aniEpType, int aniEpNumber, string traktID,
        int seasonNumber, int traktEpNumber)
    {
        var xref = RepoFactory.CrossRef_AniDB_TraktV2.GetByTraktID(traktID, seasonNumber,
            traktEpNumber, animeID,
            (int)aniEpType,
            aniEpNumber);
        if (xref == null)
        {
            return;
        }

        RepoFactory.CrossRef_AniDB_TraktV2.Delete(xref.CrossRef_AniDB_TraktV2ID);

        // Disable auto-matching when we remove an existing match for the series.
        var series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
        if (series != null)
        {
            series.IsTraktAutoMatchingDisabled = true;
            RepoFactory.AnimeSeries.Save(series, false, true, true);
        }

        SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);
    }

    public void ScanForMatches()
    {
        if (!_settingsProvider.GetSettings().TraktTv.Enabled)
        {
            return;
        }

        var allSeries = RepoFactory.AnimeSeries.GetAll();

        var allCrossRefs = RepoFactory.CrossRef_AniDB_TraktV2.GetAll();
        var alreadyLinked = new List<int>();
        foreach (var xref in allCrossRefs)
        {
            alreadyLinked.Add(xref.AnimeID);
        }

        foreach (var ser in allSeries)
        {
            if (ser.IsTraktAutoMatchingDisabled || alreadyLinked.Contains(ser.AniDB_ID))
                continue;

            var anime = ser.GetAnime();
            if (anime == null)
                continue;

            _logger.LogTrace("Found anime without Trakt association: {MaintTitle}", anime.MainTitle);

            var cmd = _commandFactory.Create<CommandRequest_TraktSearchAnime>(c => c.AnimeID = ser.AniDB_ID);
            cmd.Save();
        }
    }

    private int? GetTraktEpisodeIdV2(SVR_AnimeEpisode ep, ref string traktID, ref int season,
        ref int epNumber)
    {
        var aniep = ep?.AniDB_Episode;
        if (aniep == null)
        {
            return null;
        }

        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(aniep.AnimeID);
        if (anime == null)
        {
            return null;
        }

        return GetTraktEpisodeIdV2(anime, aniep, ref traktID, ref season, ref epNumber);
    }

    private int? GetTraktEpisodeIdV2(SVR_AniDB_Anime anime, AniDB_Episode ep, ref string traktID,
        ref int season,
        ref int epNumber)
    {
        if (anime == null || ep == null)
        {
            return null;
        }

        var traktSummary = new TraktSummaryContainer();

        traktSummary.Populate(anime.AnimeID);

        return GetTraktEpisodeIdV2(traktSummary, ep, ref traktID, ref season, ref epNumber);
    }

    private int? GetTraktEpisodeIdV2(TraktSummaryContainer traktSummary,
        AniDB_Episode ep,
        ref string traktID, ref int season, ref int epNumber)
    {
        try
        {
            if (traktSummary == null)
            {
                return null;
            }

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
                    var traktCrossRef =
                        traktSummary.CrossRefTraktV2.OrderByDescending(a => a.AniDBStartEpisodeNumber).ToList();

                    var foundStartingPoint = false;
                    CrossRef_AniDB_TraktV2 xrefBase = null;
                    foreach (var xrefTrakt in traktCrossRef)
                    {
                        if (xrefTrakt.AniDBStartEpisodeType != (int)EpisodeType.Episode)
                        {
                            continue;
                        }

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
                        foreach (var det in traktSummary.TraktDetails.Values)
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
                            var episodeNumber = dictTraktSeasons[xrefBase.TraktSeasonNumber] +
                                                (ep.EpisodeNumber + xrefBase.TraktStartEpisodeNumber - 2) -
                                                (xrefBase.AniDBStartEpisodeNumber - 1);
                            if (dictTraktEpisodes.ContainsKey(episodeNumber))
                            {
                                var traktep = dictTraktEpisodes[episodeNumber];
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
                var traktCrossRef =
                    traktSummary.CrossRefTraktV2?.OrderByDescending(a => a.AniDBStartEpisodeNumber).ToList();

                if (traktCrossRef == null)
                {
                    return null;
                }

                var foundStartingPoint = false;
                CrossRef_AniDB_TraktV2 xrefBase = null;
                foreach (var xrefTrakt in traktCrossRef)
                {
                    if (xrefTrakt.AniDBStartEpisodeType != (int)EpisodeType.Special)
                    {
                        continue;
                    }

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
                    foreach (var det in traktSummary.TraktDetails.Values)
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
                        var episodeNumber = dictTraktSeasons[xrefBase.TraktSeasonNumber] +
                                            (ep.EpisodeNumber + xrefBase.TraktStartEpisodeNumber - 2) -
                                            (xrefBase.AniDBStartEpisodeNumber - 1);
                        if (dictTraktEpisodes != null && dictTraktEpisodes.ContainsKey(episodeNumber))
                        {
                            var traktep = dictTraktEpisodes[episodeNumber];
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
            _logger.LogError(ex, "{Ex}", ex);
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
    public void UpdateAllInfo(string traktID)
    {
        GetShowInfoV2(traktID);
    }

    #region Send Data to Trakt

    public CL_Response<bool> PostCommentShow(string traktSlug, string commentText, bool isSpoiler)
    {
        var ret = new CL_Response<bool>();
        try
        {
            var settings = _settingsProvider.GetSettings();
            if (!settings.TraktTv.Enabled)
            {
                ret.ErrorMessage = "Trakt has not been enabled";
                ret.Result = false;
                return ret;
            }

            if (string.IsNullOrEmpty(settings.TraktTv.AuthToken))
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

            var comment = new TraktV2CommentShowPost();
            comment.Init(commentText, isSpoiler, traktSlug);

            var json = JSONHelper.Serialize(comment);


            var retData = string.Empty;
            TraktTVRateLimiter.Instance.EnsureRate();
            var response = SendData(TraktURIs.PostComment, json, "POST", BuildRequestHeaders(), ref retData);
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
            _logger.LogError(ex, "Error in TraktTVHelper.PostCommentShow");
            ret.ErrorMessage = ex.Message;
            ret.Result = false;
            return ret;
        }
    }

    private DateTime GetEpisodeDateForSync(SVR_AnimeEpisode ep, TraktSyncType syncType)
    {
        DateTime epDate;

        if (syncType is TraktSyncType.CollectionAdd or TraktSyncType.CollectionRemove)
        {
            epDate = DateTime.Now; // not relevant for a remove
            if (syncType != TraktSyncType.CollectionAdd)
            {
                return epDate;
            }

            // get the the first file that was added to this episode
            DateTime? thisDate = null;
            foreach (var vid in ep.GetVideoLocals())
            {
                thisDate ??= vid.DateTimeCreated;

                if (vid.DateTimeCreated < thisDate)
                {
                    thisDate = vid.DateTimeCreated;
                }
            }

            if (thisDate.HasValue)
            {
                epDate = thisDate.Value;
            }
        }
        else
        {
            epDate = DateTime.Now; // not relevant for a remove
            if (syncType != TraktSyncType.HistoryAdd)
            {
                return epDate;
            }

            // get the latest user record and find the latest date this episode was watched
            DateTime? thisDate = null;
            var traktUsers = RepoFactory.JMMUser.GetTraktUsers();
            if (traktUsers.Count <= 0)
            {
                return epDate;
            }

            foreach (var juser in traktUsers)
            {
                var userRecord = ep.GetUserRecord(juser.JMMUserID);
                if (userRecord == null)
                {
                    continue;
                }

                if (!thisDate.HasValue && userRecord.WatchedDate.HasValue)
                {
                    thisDate = userRecord.WatchedDate;
                }

                if (userRecord.WatchedDate.HasValue && thisDate.HasValue &&
                    userRecord.WatchedDate > thisDate)
                {
                    thisDate = userRecord.WatchedDate;
                }
            }

            if (thisDate.HasValue)
            {
                epDate = thisDate.Value;
            }
        }

        return epDate;
    }

    public void SyncEpisodeToTrakt(SVR_AnimeEpisode ep, TraktSyncType syncType)
    {
        try
        {
            var settings = _settingsProvider.GetSettings();
            if (!settings.TraktTv.Enabled ||
                string.IsNullOrEmpty(settings.TraktTv.AuthToken))
            {
                return;
            }

            var traktShowID = string.Empty;
            var season = -1;
            var epNumber = -1;

            GetTraktEpisodeIdV2(ep, ref traktShowID, ref season, ref epNumber);
            if (string.IsNullOrEmpty(traktShowID) || season < 0 || epNumber < 0)
            {
                return;
            }

            var epDate = GetEpisodeDateForSync(ep, syncType);
            TraktTVRateLimiter.Instance.EnsureRate();

            //SyncEpisodeToTrakt(syncType, traktEpisodeId.Value, secondaryAction);
            SyncEpisodeToTrakt(syncType, traktShowID, season, epNumber, epDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.SyncEpisodeToTrakt");
        }
    }

    private void SyncEpisodeToTrakt(TraktSyncType syncType, string slug, int season, int epNumber, DateTime epDate)
    {
        try
        {
            var settings = _settingsProvider.GetSettings();
            if (!settings.TraktTv.Enabled ||
                string.IsNullOrEmpty(settings.TraktTv.AuthToken))
            {
                return;
            }

            string json;
            if (syncType is TraktSyncType.CollectionAdd or TraktSyncType.CollectionRemove)
            {
                var sync = new TraktV2SyncCollectionEpisodesByNumber(slug, season,
                    epNumber,
                    epDate);
                json = JSONHelper.Serialize(sync);
            }
            else
            {
                var sync = new TraktV2SyncWatchedEpisodesByNumber(slug, season,
                    epNumber, epDate);
                json = JSONHelper.Serialize(sync);
            }


            var url = TraktURIs.SyncCollectionAdd;
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

            var retData = string.Empty;
            TraktTVRateLimiter.Instance.EnsureRate();
            SendData(url, json, "POST", BuildRequestHeaders(), ref retData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.SyncEpisodeToTrakt");
        }
    }

    public int Scrobble(ScrobblePlayingType scrobbleType, string animeEpisodeID, ScrobblePlayingStatus scrobbleStatus,
        float progress)
    {
        try
        {
            var settings = _settingsProvider.GetSettings();
            if (!settings.TraktTv.Enabled ||
                string.IsNullOrEmpty(settings.TraktTv.AuthToken))
            {
                return 401;
            }

            var json = string.Empty;

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
            if (!int.TryParse(animeEpisodeID, out var aep))
            {
                return 400;
            }

            var ep = RepoFactory.AnimeEpisode.GetByID(aep);
            var slugID = string.Empty;
            var season = 0;
            var epNumber = 0;
            var traktID = GetTraktEpisodeIdV2(ep, ref slugID, ref season, ref epNumber);

            //2.generate json
            if (traktID != null && traktID > 0)
            {
                switch (scrobbleType)
                {
                    case ScrobblePlayingType.episode:
                        var showE = new TraktV2ScrobbleEpisode();
                        showE.Init(progress, traktID, slugID, season, epNumber);
                        json = JSONHelper.Serialize(showE);
                        break;

                    //do we have any movies that work?
                    case ScrobblePlayingType.movie:
                        var showM = new TraktV2ScrobbleMovie();
                        json = JSONHelper.Serialize(showM);
                        showM.Init(progress, slugID, traktID.ToString());
                        break;
                }

                //3. send Json
                var retData = string.Empty;
                TraktTVRateLimiter.Instance.EnsureRate();
                SendData(url, json, "POST", BuildRequestHeaders(), ref retData);
            }
            else
            {
                //3. nothing to send log error
                _logger.LogWarning(
                    "TraktTVHelper.Scrobble: No TraktID found for: AnimeEpisodeID: {ID} AnimeRomajiName: {Title}", aep,
                    ep.Title);
                return 404;
            }

            return 200;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.Scrobble");
            return 500;
        }
    }

    #endregion

    #region Get Data From Trakt

    public List<TraktV2SearchShowResult> SearchShowV2(string criteria)
    {
        var results = new List<TraktV2SearchShowResult>();

        var settings = _settingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken))
        {
            return results;
        }

        try
        {
            // replace spaces with a + symbo
            //criteria = criteria.Replace(' ', '+');

            // Search for a series
            var url = string.Format(TraktURIs.Search, criteria, TraktSearchType.show);
            _logger.LogTrace("Search Trakt Show: {URL}", url);

            // Search for a series
            var json = GetFromTrakt(url);

            if (string.IsNullOrEmpty(json))
            {
                return new List<TraktV2SearchShowResult>();
            }

            var result = json.FromJSONArray<TraktV2SearchShowResult>();
            if (result == null)
            {
                return null;
            }

            return new List<TraktV2SearchShowResult>(result);

            // save this data for later use
            //foreach (TraktTVShow tvshow in results)
            //    SaveExtendedShowInfo(tvshow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Trakt SearchSeries");
        }

        return null;
    }

    public List<TraktV2SearchTvDBIDShowResult> SearchShowByIDV2(string idType, string id)
    {
        var results = new List<TraktV2SearchTvDBIDShowResult>();

        var settings = _settingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken))
        {
            return results;
        }

        try
        {
            // Search for a series
            var url = string.Format(TraktURIs.SearchByID, idType, id);
            _logger.LogTrace("Search Trakt Show: {Url}", url);

            // Search for a series
            var json = GetFromTrakt(url);

            if (string.IsNullOrEmpty(json))
            {
                return new List<TraktV2SearchTvDBIDShowResult>();
            }

            //var result2 = json.FromJSONArray<Class1>();
            var result = json.FromJSONArray<TraktV2SearchTvDBIDShowResult>();
            if (result == null)
            {
                return null;
            }

            return new List<TraktV2SearchTvDBIDShowResult>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SearchSeries");
        }

        return null;
    }


    public TraktV2ShowExtended GetShowInfoV2(string traktID)
    {
        var traktCode = TraktStatusCodes.Success;
        return GetShowInfoV2(traktID, ref traktCode);
    }

    private TraktV2ShowExtended GetShowInfoV2(string traktID, ref int traktCode)
    {
        TraktV2ShowExtended resultShow;

        var settings = _settingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken))
        {
            return null;
        }

        try
        {
            var url = string.Format(TraktURIs.ShowSummary, traktID);
            _logger.LogTrace("GetShowInfo: {Url}", url);

            // Search for a series
            var json = GetFromTrakt(url, ref traktCode);

            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            resultShow = json.FromJSON<TraktV2ShowExtended>();
            if (resultShow == null)
            {
                return null;
            }

            // if we got the show info, also download the seaon info
            url = string.Format(TraktURIs.ShowSeasons, traktID);
            _logger.LogTrace("GetSeasonInfo: {Url}", url);
            json = GetFromTrakt(url);

            var seasons = new List<TraktV2Season>();
            if (!string.IsNullOrEmpty(json))
            {
                var resultSeasons = json.FromJSONArray<TraktV2Season>();
                foreach (var season in resultSeasons)
                {
                    seasons.Add(season);
                }
            }

            // save this data to the DB for use later
            SaveExtendedShowInfoV2(resultShow, seasons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.GetShowInfo");
            return null;
        }

        return resultShow;
    }

    private void SaveExtendedShowInfoV2(TraktV2ShowExtended tvshow, List<TraktV2Season> seasons)
    {
        try
        {
            // save this data to the DB for use later
            var show = RepoFactory.Trakt_Show.GetByTraktSlug(tvshow.ids.slug) ?? new Trakt_Show();

            show.Populate(tvshow);
            RepoFactory.Trakt_Show.Save(show);

            // save the seasons

            // delete episodes if they no longer exist on Trakt
            if (seasons.Count > 0)
            {
                foreach (var epTemp in RepoFactory.Trakt_Episode.GetByShowID(show.Trakt_ShowID))
                {
                    TraktV2Episode ep = null;
                    var sea = seasons.FirstOrDefault(x => x.number == epTemp.Season);
                    if (sea != null)
                    {
                        ep = sea.episodes.FirstOrDefault(x => x.number == epTemp.EpisodeNumber);
                    }

                    // if the episode is null, it means it doesn't exist on Trakt, so we should delete it
                    if (ep == null)
                    {
                        RepoFactory.Trakt_Episode.Delete(epTemp.Trakt_EpisodeID);
                    }
                }
            }

            foreach (var sea in seasons)
            {
                var season = RepoFactory.Trakt_Season.GetByShowIDAndSeason(show.Trakt_ShowID, sea.number) ??
                             new Trakt_Season();

                season.Season = sea.number;
                season.URL = string.Format(TraktURIs.WebsiteSeason, show.TraktID, sea.number);
                season.Trakt_ShowID = show.Trakt_ShowID;
                RepoFactory.Trakt_Season.Save(season);

                if (sea.episodes == null)
                {
                    continue;
                }

                foreach (var ep in sea.episodes)
                {
                    var episode = RepoFactory.Trakt_Episode.GetByShowIDSeasonAndEpisode(
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.SaveExtendedShowInfo");
        }
    }

    public List<TraktV2Comment> GetShowCommentsV2(int animeID)
    {
        using var session = DatabaseFactory.SessionFactory.OpenSession();
        return GetShowCommentsV2(session, animeID);
    }

    private List<TraktV2Comment> GetShowCommentsV2(ISession session, int animeID)
    {
        var ret = new List<TraktV2Comment>();
        try
        {
            var settings = _settingsProvider.GetSettings();
            if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken))
            {
                return ret;
            }

            var traktXRefs =
                RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(session, animeID);
            if (traktXRefs == null || traktXRefs.Count == 0)
            {
                return null;
            }

            // get a unique list of trakt id's
            var ids = new List<string>();
            foreach (var xref in traktXRefs)
            {
                if (!ids.Contains(xref.TraktID))
                {
                    ids.Add(xref.TraktID);
                }
            }

            foreach (var id in ids)
            {
                var morePages = true;
                var curPage = 0;

                while (morePages)
                {
                    curPage++;
                    var url = string.Format(TraktURIs.ShowComments, id, curPage, TraktConstants.PaginationLimit);
                    _logger.LogTrace("GetShowComments: {Url}", url);

                    var json = GetFromTrakt(url);

                    if (string.IsNullOrEmpty(json))
                    {
                        return null;
                    }

                    var resultComments = json.FromJSONArray<TraktV2Comment>();
                    if (resultComments != null)
                    {
                        var thisComments = new List<TraktV2Comment>(resultComments);
                        ret.AddRange(thisComments);

                        if (thisComments.Count != TraktConstants.PaginationLimit)
                        {
                            morePages = false;
                        }
                    }
                    else
                    {
                        morePages = false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.GetShowComments");
        }

        return ret;
    }

    private List<TraktV2ShowWatchedResult> GetWatchedShows(ref int traktCode)
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken))
        {
            return new List<TraktV2ShowWatchedResult>();
        }

        try
        {
            // Search for a series
            var url = string.Format(TraktURIs.GetWatchedShows);
            _logger.LogTrace("Get All Watched Shows and Episodes: {Url}", url);

            // Search for a series
            var json = GetFromTrakt(url, ref traktCode);

            if (string.IsNullOrEmpty(json))
            {
                return new List<TraktV2ShowWatchedResult>();
            }

            var result = json.FromJSONArray<TraktV2ShowWatchedResult>();
            if (result == null)
            {
                return new List<TraktV2ShowWatchedResult>();
            }

            return new List<TraktV2ShowWatchedResult>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SearchSeries");
        }

        return new List<TraktV2ShowWatchedResult>();
    }

    public List<TraktV2ShowCollectedResult> GetCollectedShows(ref int traktCode)
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken))
        {
            return new List<TraktV2ShowCollectedResult>();
        }

        try
        {
            // Search for a series
            var url = string.Format(TraktURIs.GetCollectedShows);
            _logger.LogTrace("Get All Collected Shows and Episodes: {0}", url);

            // Search for a series
            var json = GetFromTrakt(url, ref traktCode);

            if (string.IsNullOrEmpty(json))
            {
                return new List<TraktV2ShowCollectedResult>();
            }


            var result = json.FromJSONArray<TraktV2ShowCollectedResult>();
            if (result == null)
            {
                return new List<TraktV2ShowCollectedResult>();
            }

            return new List<TraktV2ShowCollectedResult>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SearchSeries");
        }

        return new List<TraktV2ShowCollectedResult>();
    }

    #endregion

    public void UpdateAllInfo()
    {
        if (!_settingsProvider.GetSettings().TraktTv.Enabled)
        {
            return;
        }

        var allCrossRefs = RepoFactory.CrossRef_AniDB_TraktV2.GetAll();
        foreach (var xref in allCrossRefs)
        {
            var cmd = _commandFactory.Create<CommandRequest_TraktUpdateInfo>(c => c.TraktID = xref.TraktID);
            cmd.Save();
        }
    }

    public void SyncCollectionToTrakt_Series(SVR_AnimeSeries series)
    {
        try
        {
            // check that we have at least one user nominated for Trakt
            var traktUsers = RepoFactory.JMMUser.GetTraktUsers();
            if (traktUsers.Count == 0)
            {
                return;
            }

            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(series.AniDB_ID);
            if (anime == null)
            {
                return;
            }

            var traktSummary = new TraktSummaryContainer();
            traktSummary.Populate(series.AniDB_ID);
            if (traktSummary.CrossRefTraktV2 == null || traktSummary.CrossRefTraktV2.Count == 0)
            {
                return;
            }

            // now get the full users collection from Trakt
            var collected = new List<TraktV2ShowCollectedResult>();
            var watched = new List<TraktV2ShowWatchedResult>();

            if (!GetTraktCollectionInfo(ref collected, ref watched))
            {
                return;
            }

            foreach (var ep in series.GetAnimeEpisodes())
            {
                if (ep.EpisodeTypeEnum is not (EpisodeType.Episode or EpisodeType.Special))
                {
                    continue;
                }

                ReconSyncTraktEpisode(series, ep, traktUsers, collected, watched, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.SyncCollectionToTrakt_Series");
        }
    }

    public void SyncCollectionToTrakt()
    {
        try
        {
            var settings = _settingsProvider.GetSettings();
            if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken))
            {
                return;
            }

            // check that we have at least one user nominated for Trakt
            var traktUsers = RepoFactory.JMMUser.GetTraktUsers();
            if (traktUsers.Count == 0)
            {
                return;
            }

            var allSeries = RepoFactory.AnimeSeries.GetAll();

            // now get the full users collection from Trakt
            var collected = new List<TraktV2ShowCollectedResult>();
            var watched = new List<TraktV2ShowWatchedResult>();

            if (!GetTraktCollectionInfo(ref collected, ref watched))
            {
                return;
            }

            var syncCollectionAdd = new TraktV2SyncCollectionEpisodesByNumber();
            var syncCollectionRemove =
                new TraktV2SyncCollectionEpisodesByNumber();
            var syncHistoryAdd = new TraktV2SyncWatchedEpisodesByNumber();
            var syncHistoryRemove = new TraktV2SyncWatchedEpisodesByNumber();

            #region Local Collection Sync

            ///////////////////////////////////////////////////////////////////////////////////////
            // First take a look at our local collection and update on Trakt
            ///////////////////////////////////////////////////////////////////////////////////////

            var counter = 0;
            foreach (var series in allSeries)
            {
                counter++;
                _logger.LogTrace("Syncing check -  local collection: {Counter} / {Count} - {Name}", counter,
                    allSeries.Count,
                    series.GetSeriesName());

                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(series.AniDB_ID);
                if (anime == null)
                {
                    continue;
                }

                //if (anime.AnimeID != 3427) continue;

                var traktSummary = new TraktSummaryContainer();
                traktSummary.Populate(series.AniDB_ID);
                if (traktSummary.CrossRefTraktV2 == null || traktSummary.CrossRefTraktV2.Count == 0)
                {
                    continue;
                }

                // get the current watched records for this series on Trakt

                foreach (var ep in series.GetAnimeEpisodes())
                {
                    if (ep.EpisodeTypeEnum is not EpisodeType.Episode and not EpisodeType.Special)
                    {
                        continue;
                    }

                    var epsync = ReconSyncTraktEpisode(series, ep, traktUsers,
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

            #endregion

            // refresh online info, just in case it was chnaged by the last operations
            if (!GetTraktCollectionInfo(ref collected, ref watched))
            {
                return;
            }

            #region Online Collection Sync

            ///////////////////////////////////////////////////////////////////////////////////////
            // Now look at the collection according to Trakt, and remove it if we don't have it locally
            ///////////////////////////////////////////////////////////////////////////////////////


            counter = 0;
            foreach (var col in collected)
            {
                counter++;
                _logger.LogTrace("Syncing check - Online collection: {Counter} / {Count} - {Title}", counter,
                    collected.Count,
                    col.show.Title);
                //continue;

                // check if we have this series locally
                var xrefs =
                    RepoFactory.CrossRef_AniDB_TraktV2.GetByTraktID(col.show.ids.slug);

                if (xrefs.Count <= 0)
                {
                    continue;
                }

                foreach (var xref in xrefs)
                {
                    var locSeries = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID);
                    if (locSeries == null)
                    {
                        continue;
                    }

                    var traktSummary = new TraktSummaryContainer();
                    traktSummary.Populate(locSeries.AniDB_ID);
                    if (traktSummary.CrossRefTraktV2 == null || traktSummary.CrossRefTraktV2.Count == 0)
                    {
                        continue;
                    }

                    // if we have this series locSeries, let's sync the whole series
                    foreach (var ep in locSeries.GetAnimeEpisodes())
                    {
                        if (ep.EpisodeTypeEnum is not EpisodeType.Episode and not EpisodeType.Special)
                        {
                            continue;
                        }

                        var epsync = ReconSyncTraktEpisode(locSeries, ep,
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

            #endregion

            // refresh online info, just in case it was chnaged by the last operations
            if (!GetTraktCollectionInfo(ref collected, ref watched))
            {
                return;
            }

            #region Online History (Watched/Unwatched) Sync

            ///////////////////////////////////////////////////////////////////////////////////////
            // Now look at the history according to Trakt, and remove it if we don't have it locally
            ///////////////////////////////////////////////////////////////////////////////////////

            counter = 0;

            foreach (var wtch in watched)
            {
                counter++;
                _logger.LogTrace("Syncing check - Online History: {Counter} / {Count} - {Title}", counter,
                    watched.Count, wtch.show.Title);

                // check if we have this series locally
                var xrefs =
                    RepoFactory.CrossRef_AniDB_TraktV2.GetByTraktID(wtch.show.ids.slug);

                if (xrefs.Count <= 0)
                {
                    continue;
                }

                foreach (var xref in xrefs)
                {
                    var locSeries = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID);
                    if (locSeries == null)
                    {
                        continue;
                    }

                    var traktSummary = new TraktSummaryContainer();
                    traktSummary.Populate(locSeries.AniDB_ID);
                    if (traktSummary.CrossRefTraktV2 == null || traktSummary.CrossRefTraktV2.Count == 0)
                    {
                        continue;
                    }

                    // if we have this series locSeries, let's sync the whole series
                    foreach (var ep in locSeries.GetAnimeEpisodes())
                    {
                        if (ep.EpisodeTypeEnum is not EpisodeType.Episode and not EpisodeType.Special)
                        {
                            continue;
                        }

                        var epsync = ReconSyncTraktEpisode(locSeries, ep, traktUsers, collected, watched, false);
                        if (epsync == null)
                        {
                            continue;
                        }

                        switch (epsync.SyncType)
                        {
                            case TraktSyncType.CollectionAdd:
                                syncCollectionAdd.AddEpisode(
                                    epsync.Slug, epsync.Season,
                                    epsync.EpNumber,
                                    epsync.EpDate
                                );
                                break;
                            case TraktSyncType.CollectionRemove:
                                syncCollectionRemove.AddEpisode(
                                    epsync.Slug, epsync.Season,
                                    epsync.EpNumber, epsync.EpDate
                                );
                                break;
                            case TraktSyncType.HistoryAdd:
                                syncHistoryAdd.AddEpisode(
                                    epsync.Slug, epsync.Season, epsync.EpNumber,
                                    epsync.EpDate
                                );
                                break;
                            case TraktSyncType.HistoryRemove:
                                syncHistoryRemove.AddEpisode(
                                    epsync.Slug, epsync.Season,
                                    epsync.EpNumber,
                                    epsync.EpDate
                                );
                                break;
                        }
                    }
                }
            }

            #endregion

            // send the data to Trakt
            string json;
            string url;
            string retData;

            if (syncCollectionAdd.shows is { Count: > 0 })
            {
                json = JSONHelper.Serialize(syncCollectionAdd);
                url = TraktURIs.SyncCollectionAdd;
                retData = string.Empty;
                TraktTVRateLimiter.Instance.EnsureRate();
                SendData(url, json, "POST", BuildRequestHeaders(), ref retData);
            }

            if (syncCollectionRemove.shows is { Count: > 0 })
            {
                json = JSONHelper.Serialize(syncCollectionRemove);
                url = TraktURIs.SyncCollectionRemove;
                retData = string.Empty;
                TraktTVRateLimiter.Instance.EnsureRate();
                SendData(url, json, "POST", BuildRequestHeaders(), ref retData);
            }

            if (syncHistoryAdd.shows is { Count: > 0 })
            {
                json = JSONHelper.Serialize(syncHistoryAdd);
                url = TraktURIs.SyncHistoryAdd;
                retData = string.Empty;
                TraktTVRateLimiter.Instance.EnsureRate();
                SendData(url, json, "POST", BuildRequestHeaders(), ref retData);
            }

            if (syncHistoryRemove.shows is { Count: > 0 })
            {
                json = JSONHelper.Serialize(syncHistoryRemove);
                url = TraktURIs.SyncHistoryRemove;
                retData = string.Empty;
                TraktTVRateLimiter.Instance.EnsureRate();
                SendData(url, json, "POST", BuildRequestHeaders(), ref retData);
            }


            _logger.LogTrace("Test");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.SyncCollectionToTrakt");
        }
    }

    public bool CheckTraktValidity(string slug, bool removeDBEntries)
    {
        try
        {
            // let's check if we can get this show on Trakt
            var traktCode = TraktStatusCodes.Success;
            // get all the shows from the database and make sure they are still valid Trakt Slugs
            var show = RepoFactory.Trakt_Show.GetByTraktSlug(slug);
            if (show == null)
            {
                _logger.LogError("Unable to get Trakt Show for \"{Slug}\". Attempting to download info, anyway", slug);
                var tempShow = GetShowInfoV2(slug, ref traktCode);
                if (tempShow == null || traktCode == TraktStatusCodes.Not_Found)
                {
                    _logger.LogError("\"{Slug}\" was not found on Trakt. Not continuing", slug);
                    return false;
                }

                show = RepoFactory.Trakt_Show.GetByTraktSlug(tempShow.ids.slug);
            }

            // note - getting extended show info also updates it as well
            var showOnline = GetShowInfoV2(show.TraktID, ref traktCode);
            if (showOnline != null || traktCode != TraktStatusCodes.Not_Found)
            {
                return true;
            }

            if (!removeDBEntries)
            {
                return false;
            }

            _logger.LogInformation(
                "TRAKT_CLEANUP: Could not find '{TraktID}' on Trakt so starting removal from database", show.TraktID);
            RemoveTraktDBEntries(show);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.CleanupDatabase");
            return false;
        }
    }

    private void RemoveTraktDBEntries(Trakt_Show show)
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

    private EpisodeSyncDetails ReconSyncTraktEpisode(SVR_AnimeSeries ser, SVR_AnimeEpisode ep,
        List<SVR_JMMUser> traktUsers, List<TraktV2ShowCollectedResult> collected,
        List<TraktV2ShowWatchedResult> watched, bool sendNow)
    {
        try
        {
            // get the Trakt Show ID for this episode
            var traktShowID = string.Empty;
            var season = -1;
            var epNumber = -1;

            GetTraktEpisodeIdV2(ep, ref traktShowID, ref season, ref epNumber);
            if (string.IsNullOrEmpty(traktShowID) || season < 0 || epNumber < 0)
            {
                return null;
            }

            // get the current collected records for this series on Trakt
            TraktV2CollectedEpisode epTraktCol = null;
            var col = collected.FirstOrDefault(x => x.show.ids.slug == traktShowID);
            if (col != null)
            {
                var sea = col.seasons.FirstOrDefault(x => x.number == season);
                if (sea != null)
                {
                    epTraktCol = sea.episodes.FirstOrDefault(x => x.number == epNumber);
                }
            }

            var onlineCollection = epTraktCol != null;

            // get the current watched records for this series on Trakt
            TraktV2WatchedEpisode epTraktWatched = null;
            var wtc = watched.FirstOrDefault(x => x.show.ids.slug == traktShowID);
            if (wtc != null)
            {
                var sea = wtc.seasons.FirstOrDefault(x => x.number == season);
                if (sea != null)
                {
                    epTraktWatched = sea.episodes.FirstOrDefault(x => x.number == epNumber);
                }
            }

            var onlineWatched = epTraktWatched != null;

            var localCollection = false;
            var localWatched = false;

            // If we have local files check for watched count
            if (ep.GetVideoLocals().Count > 0)
            {
                localCollection = true;

                foreach (var juser in traktUsers)
                {
                    // If there's a watch count we mark it as locally watched
                    if (ep.GetUserRecord(juser.JMMUserID)?.WatchedCount > 0)
                    {
                        localWatched = true;
                    }
                }
            }

            _logger.LogTrace(
                "Sync Check Status:  AniDB: {ShowID} - {EpisodeTypeEnum} - {EpisodeID} - Collection: {LocalCollection} - Watched: {Watched}",
                ser.AniDB_ID, ep.EpisodeTypeEnum, ep.AniDB_EpisodeID, localCollection, localWatched);
            _logger.LogTrace(
                "Sync Check Status:  Trakt: {ShowID} - S:{Season} - EP:{EpNumber} - Collection: {OnlineCollection} - Watched: {Watched}",
                traktShowID, season, epNumber, onlineCollection, onlineWatched);

            // sync the collection status
            if (localCollection)
            {
                // is in the local collection, but not Trakt, so let's ADD it
                if (!onlineCollection)
                {
                    _logger.LogTrace(
                        "SYNC LOCAL: Adding to Trakt Collection:  Slug: {ShowID} - S:{Season} - EP:{EpNumber}",
                        traktShowID, season, epNumber);
                    var epDate = GetEpisodeDateForSync(ep, TraktSyncType.CollectionAdd);
                    if (sendNow)
                    {
                        SyncEpisodeToTrakt(TraktSyncType.CollectionAdd, traktShowID, season, epNumber, epDate);
                    }
                    else
                    {
                        return new EpisodeSyncDetails(TraktSyncType.CollectionAdd, traktShowID, season, epNumber,
                            epDate);
                    }
                }
            }
            else
            {
                // is in the trakt collection, but not local, so let's REMOVE it
                if (onlineCollection)
                {
                    _logger.LogTrace(
                        "SYNC LOCAL: Removing from Trakt Collection:  Slug: {ShowID} - S:{Season} - EP:{EpNumber}",
                        traktShowID, season, epNumber);
                    var epDate = GetEpisodeDateForSync(ep, TraktSyncType.CollectionRemove);
                    if (sendNow)
                    {
                        SyncEpisodeToTrakt(TraktSyncType.CollectionRemove, traktShowID, season, epNumber, epDate);
                    }
                    else
                    {
                        return new EpisodeSyncDetails(TraktSyncType.CollectionRemove, traktShowID, season, epNumber,
                            epDate);
                    }
                }
            }

            // sync the watched status
            if (localWatched)
            {
                // is watched locally, but not Trakt, so let's ADD it
                if (onlineWatched)
                {
                    return null;
                }

                _logger.LogTrace(
                    "SYNC LOCAL: Adding to Trakt History:  Slug: {TraktShowID} - S:{Season} - EP:{EpNumber}",
                    traktShowID, season, epNumber);
                var epDate = GetEpisodeDateForSync(ep, TraktSyncType.HistoryAdd);
                if (sendNow)
                {
                    SyncEpisodeToTrakt(TraktSyncType.HistoryAdd, traktShowID, season, epNumber, epDate);
                }
                else
                {
                    return new EpisodeSyncDetails(TraktSyncType.HistoryAdd, traktShowID, season, epNumber,
                        epDate);
                }
            }
            else
            {
                // is watched on trakt, but not locally, so let's REMOVE it
                if (!onlineWatched)
                {
                    return null;
                }

                _logger.LogTrace(
                    "SYNC LOCAL: Removing from Trakt History:  Slug: {TraktShowID} - S:{Season} - EP:{EpNumber}",
                    traktShowID, season, epNumber);
                var epDate = GetEpisodeDateForSync(ep, TraktSyncType.HistoryRemove);
                if (sendNow)
                {
                    SyncEpisodeToTrakt(TraktSyncType.HistoryRemove, traktShowID, season, epNumber, epDate);
                }
                else
                {
                    return new EpisodeSyncDetails(TraktSyncType.HistoryRemove, traktShowID, season, epNumber,
                        epDate);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.SyncTraktEpisode");
            return null;
        }
    }

    private bool GetTraktCollectionInfo(ref List<TraktV2ShowCollectedResult> collected,
        ref List<TraktV2ShowWatchedResult> watched)
    {
        try
        {
            var settings = _settingsProvider.GetSettings();
            if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken))
            {
                return false;
            }

            // check that we have at least one user nominated for Trakt
            var traktUsers = RepoFactory.JMMUser.GetTraktUsers();
            if (traktUsers.Count == 0)
            {
                return false;
            }

            var traktCode = TraktStatusCodes.Success;

            // now get the full users collection from Trakt
            collected = GetCollectedShows(ref traktCode);
            if (traktCode != TraktStatusCodes.Success)
            {
                _logger.LogError("Could not get users collection: {TraktCode}", traktCode);
                return false;
            }

            // now get all the shows / episodes the user has watched
            watched = GetWatchedShows(ref traktCode);
            if (traktCode == TraktStatusCodes.Success)
            {
                return true;
            }

            _logger.LogError("Could not get users watched history: {TraktCode}", traktCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.GetTraktCollectionInfo");
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
