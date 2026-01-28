using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV.Contracts;
using Shoko.Server.Providers.TraktTV.Contracts.Scrobble;
using Shoko.Server.Providers.TraktTV.Contracts.Sync;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#pragma warning disable SYSLIB0014
#nullable enable
namespace Shoko.Server.Providers.TraktTV;

public class TraktTVHelper
{
    private readonly ILogger<TraktTVHelper> _logger;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IUserDataService _userDataService;

    public TraktTVHelper(ILogger<TraktTVHelper> logger, ISettingsProvider settingsProvider, IUserDataService userDataService)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _userDataService = userDataService;
    }

    #region Helpers

    private int SendData(string uri, string json, string verb, Dictionary<string, string> headers, ref string webResponse)
    {
        var ret = 400;

        try
        {
            var data = new UTF8Encoding().GetBytes(json);
            _logger.LogTrace("Trakt SEND Data\nVerb: {Verb}\nuri: {Uri}\njson: {Json}", verb, uri, json);

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
                    if (response.ResponseUri.AbsoluteUri != TraktURIs.OAuthDeviceToken && response.StatusCode == HttpStatusCode.BadRequest)
                    {
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
            if (webEx.Response != null && webEx.Response.ResponseUri.AbsoluteUri != TraktURIs.OAuthDeviceToken)
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

    private string GetFromTrakt(string uri, ref int traktCode)
    {
        var request = (HttpWebRequest)WebRequest.Create(uri);

        _logger.LogTrace("Trakt GET Data\nuri: {Uri}", uri);

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
                return string.Empty;
            }

            var reader = new StreamReader(stream);
            var strResponse = reader.ReadToEnd();

            stream.Close();
            reader.Close();
            response.Close();

            // log the response unless it is Full Watched as this data is way too big
            if (!uri.Equals(TraktURIs.GetWatchedShows, StringComparison.InvariantCultureIgnoreCase) &&
                !uri.Equals(TraktURIs.GetWatchedMovies, StringComparison.InvariantCultureIgnoreCase))
            {
                _logger.LogTrace("Trakt GET Data - Response\nResponse: {Response}", strResponse);
            }

            return strResponse;
        }
        catch (WebException e)
        {
            _logger.LogError(e, "Error in GetFromTrakt");

            var httpResponse = (HttpWebResponse)e.Response;
            traktCode = (int?)httpResponse?.StatusCode ?? 0;

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetFromTrakt");
            return string.Empty;
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

    public bool RefreshAuthToken()
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

                return false;
            }

            var token = new TraktV2RefreshToken { refresh_token = settings.TraktTv.RefreshToken };
            var json = JsonConvert.SerializeObject(token);
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

                return true;
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
            return false;
        }
        finally
        {
            Utils.SettingsProvider.SaveSettings();
        }
        return false;
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
            var json = JsonConvert.SerializeObject(obj);
            var headers = new Dictionary<string, string>();

            var retData = string.Empty;
            TraktTVRateLimiter.Instance.EnsureRate();
            var response = SendData(TraktURIs.OAuthDeviceCode, json, "POST", headers, ref retData);
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
            var json = JsonConvert.SerializeObject(obj);
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

                switch (response)
                {
                    case TraktStatusCodes.Rate_Limit_Exceeded:
                        //Temporarily increase poll interval
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                        break;
                    case TraktStatusCodes.Awaiting_Auth:
                        // Signaling the user that auth is still pending
                        _logger.LogInformation(response, "Authorization for Shoko pending, please enter the code displayed by clicking the link");
                        break;
                    case TraktStatusCodes.Token_Expired:
                        // Signaling the user that Token has expired and restart is needed
                        _logger.LogInformation(response, "Trakt token has expired, please restart the pairing process");
                        break;
                    default:
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

    #region Send Data to Trakt

    public void SendEpisodeWatchState(TraktSyncType syncType, SVR_AnimeEpisode episode, DateTime? date = null)
    {
        try
        {
            _logger.LogInformation("Trakt: Sending watch states for {Anime} - {EpisodeType} {EpisodeNumber}",
                episode.AnimeSeries?.PreferredTitle, episode.EpisodeTypeEnum, episode.AniDB_Episode?.EpisodeNumber);

            if (episode.TmdbEpisodes.Count == 0 && episode.TmdbMovies.Count == 0)
            {
                return;
            }

            var url = syncType switch
            {
                TraktSyncType.HistoryAdd => TraktURIs.SyncHistoryAdd,
                TraktSyncType.HistoryRemove => TraktURIs.SyncHistoryRemove,
                _ => TraktURIs.SyncHistoryAdd
            };

            var tmdbEpisodeIds = GetTmdbEpisodeIdsFromEpisode(episode);
            var tmdbMovieIds = GetTmdbMovieIdsFromEpisode(episode);

            var sync = new TraktSync
            {
                Episodes = tmdbEpisodeIds.Select(x => CreateHistoryItemFromTmdbId(x, date)).ToList(),
                Movies = tmdbMovieIds.Select(x => CreateHistoryItemFromTmdbId(x, date)).ToList()
            };

            var retData = string.Empty;
            var json = JsonConvert.SerializeObject(sync);
            TraktTVRateLimiter.Instance.EnsureRate();
            SendData(url, json, "POST", BuildRequestHeaders(), ref retData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.SendEpisodeWatchState");
        }
    }

    private void SyncHistory(TraktSyncType syncType, TraktSync syncItems)
    {
        try
        {
            _logger.LogInformation("Trakt: {Operation} watch states for {EpisodeCount} episodes and {MovieCount} movies",
                syncType is TraktSyncType.HistoryAdd ? "Sending" : "Removing", syncItems.Episodes.Count, syncItems.Movies.Count);

            var json = JsonConvert.SerializeObject(syncItems);
            var url = syncType is TraktSyncType.HistoryAdd ? TraktURIs.SyncHistoryAdd : TraktURIs.SyncHistoryRemove;
            var retData = string.Empty;
            TraktTVRateLimiter.Instance.EnsureRate();
            SendData(url, json, "POST", BuildRequestHeaders(), ref retData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.SyncHistory");
        }
    }

    public int Scrobble(SVR_AnimeEpisode episode, ScrobblePlayingStatus scrobbleStatus, float progress)
    {
        try
        {
            var settings = _settingsProvider.GetSettings();
            if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken))
            {
                return 401;
            }

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

            // TODO: Figure out scrobbling if the episode has multiple links
            var tmdbEpisodeIds = GetTmdbEpisodeIdsFromEpisode(episode);
            var tmdbMovieIds = GetTmdbMovieIdsFromEpisode(episode);

            if (tmdbEpisodeIds.Count == 0 && tmdbMovieIds.Count == 0)
            {
                _logger.LogWarning(
                    "TraktTVHelper.Scrobble: No TMDB IDs found for: Anime Episode ID: {ID} Anime: {Title}", episode.AnimeEpisodeID,
                    episode.PreferredTitle);
                return 404;
            }

            var retData = string.Empty;
            var scrobble = new TraktScrobble
            {
                Progress = progress
            };

            if (tmdbEpisodeIds.Count > 0)
            {
                scrobble.Episode = new TraktScrobbleItem
                {
                    Ids = new TraktIds
                    {
                        TmdbID = tmdbEpisodeIds.First()
                    }
                };
            }
            else
            {
                scrobble.Movie = new TraktScrobbleItem
                {
                    Ids = new TraktIds
                    {
                        TmdbID = tmdbEpisodeIds.First()
                    }
                };
            }

            var json = JsonConvert.SerializeObject(scrobble);
            TraktTVRateLimiter.Instance.EnsureRate();
            SendData(url, json, "POST", BuildRequestHeaders(), ref retData);

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

    private bool GetWatchedShowsInfo(ref Dictionary<int, TraktSyncWatchedShowsResult> watchedShows)
    {
        try
        {
            var traktCode = TraktStatusCodes.Success;

            // now get all the shows / episodes the user has watched
            var watchedShowsResult = GetWatchedShows(ref traktCode);
            if (traktCode != TraktStatusCodes.Success)
            {
                _logger.LogError("Trakt: Unable to retrieve watched shows information: {TraktCode}", traktCode);
                return false;
            }

            watchedShows = watchedShowsResult
                .Where(x => x.Show.IDs.TmdbID.HasValue)
                .ToDictionary(x => x.Show.IDs.TmdbID!.Value);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.GetWatchedShowsInfo");
            return false;
        }
    }

    private bool GetWatchedMoviesInfo(ref Dictionary<int, TraktSyncWatchedMoviesResult> watchedMovies)
    {
        try
        {
            var traktCode = TraktStatusCodes.Success;

            var watchedMoviesResult = GetWatchedMovies(ref traktCode);
            if (traktCode != TraktStatusCodes.Success)
            {
                _logger.LogError("Trakt: Unable to retrieve watched movies information: {TraktCode}", traktCode);
                return false;
            }

            watchedMovies = watchedMoviesResult
                .Where(x => x.Movie.IDs.TmdbID.HasValue)
                .ToDictionary(x => x.Movie.IDs.TmdbID!.Value);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.GetWatchedMoviesInfo");
            return false;
        }
    }

    private List<TraktSyncWatchedShowsResult> GetWatchedShows(ref int traktCode)
    {
        try
        {
            // Search for a series
            var url = string.Format(TraktURIs.GetWatchedShows);
            _logger.LogTrace("Get All Watched Shows and Episodes: {Url}", url);

            // Search for a series
            var json = GetFromTrakt(url, ref traktCode);

            if (string.IsNullOrEmpty(json))
            {
                return [];
            }

            var result = json.FromJSONArray<TraktSyncWatchedShowsResult>();
            return result == null ? [] : result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.GetWatchedShows");
        }

        return [];
    }

    private List<TraktSyncWatchedMoviesResult> GetWatchedMovies(ref int traktCode)
    {
        try
        {
            // Search for a series
            var url = string.Format(TraktURIs.GetWatchedMovies);
            _logger.LogTrace("Get All Watched Movies: {Url}", url);

            // Search for a series
            var json = GetFromTrakt(url, ref traktCode);

            if (string.IsNullOrEmpty(json))
            {
                return [];
            }

            var result = json.FromJSONArray<TraktSyncWatchedMoviesResult>();
            return result == null ? [] : result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.GetWatchedMovies");
        }

        return [];
    }

    #endregion

    public void SendWatchStates(SVR_AnimeSeries? watchedSeries = null)
    {
        try
        {
            _logger.LogInformation("Trakt: Sending watch states");

            // check that we have at least one user nominated for Trakt
            var traktEnabledUser = RepoFactory.JMMUser.GetTraktUsers().FirstOrDefault();
            if (traktEnabledUser is null)
            {
                _logger.LogError("Trakt: No trakt users found!");
                return;
            }

            IReadOnlyList<SVR_AnimeSeries> allSeries;

            if (watchedSeries is not null)
            {
                _logger.LogInformation("Trakt: Sending watch states for series {Anime}", watchedSeries.PreferredTitle);
                allSeries = new List<SVR_AnimeSeries>
                {
                    watchedSeries
                };
            }
            else
            {
                _logger.LogInformation("Trakt: Sending watch states for whole collection");
                allSeries = RepoFactory.AnimeSeries.GetAll();
            }

            var watchedShows = new Dictionary<int, TraktSyncWatchedShowsResult>();
            var watchedMovies = new Dictionary<int, TraktSyncWatchedMoviesResult>();

            if (!GetWatchedShowsInfo(ref watchedShows) || !GetWatchedMoviesInfo(ref watchedMovies))
                return;

            var syncHistoryAdd = new TraktSync();
            var syncHistoryRemove = new TraktSync();

            var counter = 0;
            foreach (var series in allSeries)
            {
                counter++;
                _logger.LogTrace("Trakt: Comparing watch states for series {Count} / {Total}. Current series: {Anime}",
                    counter, allSeries.Count, series.PreferredTitle);

                if (series.TmdbEpisodeCrossReferences.Count == 0 && series.TmdbMovieCrossReferences.Count == 0)
                {
                    _logger.LogTrace("Trakt: No TMDB links found for {Anime}", series.PreferredTitle);
                    continue;
                }

                // get the current watched records for this series on Trakt, ignore episodes with no files
                var episodesWithFiles = series.AllAnimeEpisodes.Where(ep => ep.VideoLocals.Count > 0);
                foreach (var episode in episodesWithFiles)
                {
                    _logger.LogTrace("Trakt: Start comparing watch states for {Anime} - {EpisodeType} {Episode}",
                        series.PreferredTitle, episode.EpisodeTypeEnum, episode.AniDB_Episode?.EpisodeNumber);
                    var episodeSyncDetailsList = CompareWatchStates(traktEnabledUser, series, episode, watchedShows, watchedMovies);

                    var episodeHistoryAdd = new List<TraktSyncHistoryItem>();
                    var episodeHistoryRemove = new List<TraktSyncHistoryItem>();
                    var movieHistoryAdd = new List<TraktSyncHistoryItem>();
                    var movieHistoryRemove = new List<TraktSyncHistoryItem>();

                    foreach (var item in episodeSyncDetailsList)
                    {
                        foreach (var id in item.TmdbEpisodeIds)
                        {
                            switch (item.SyncType)
                            {
                                case TraktSyncType.HistoryAdd:
                                    episodeHistoryAdd.Add(CreateHistoryItemFromTmdbId(id, item.EpDate));
                                    break;
                                case TraktSyncType.HistoryRemove:
                                    episodeHistoryRemove.Add(CreateHistoryItemFromTmdbId(id, item.EpDate));
                                    break;
                            }
                        }

                        foreach (var id in item.TmdbMovieIds)
                        {
                            switch (item.SyncType)
                            {
                                case TraktSyncType.HistoryAdd:
                                    movieHistoryAdd.Add(CreateHistoryItemFromTmdbId(id, item.EpDate));
                                    break;
                                case TraktSyncType.HistoryRemove:
                                    movieHistoryRemove.Add(CreateHistoryItemFromTmdbId(id, item.EpDate));
                                    break;
                            }
                        }
                    }

                    _logger.LogTrace("Trakt: Compared watch states for {Anime} - {EpisodeType} {Episode}\nWatch states to send: {EpisodeCount} episodes, {MovieCount} movies",
                        series.PreferredTitle, episode.EpisodeTypeEnum, episode.AniDB_Episode?.EpisodeNumber, episodeHistoryAdd.Count, movieHistoryAdd.Count);

                    syncHistoryAdd.Episodes.AddRange(episodeHistoryAdd);
                    syncHistoryAdd.Movies.AddRange(movieHistoryAdd);
                    syncHistoryRemove.Episodes.AddRange(episodeHistoryRemove);
                    syncHistoryRemove.Movies.AddRange(movieHistoryRemove);
                }
            }

            if (syncHistoryAdd.Episodes.Count > 0 || syncHistoryAdd.Movies.Count > 0)
            {
                SyncHistory(TraktSyncType.HistoryAdd, syncHistoryAdd);
            }

            if (syncHistoryRemove.Episodes.Count > 0 || syncHistoryRemove.Movies.Count > 0)
            {
                SyncHistory(TraktSyncType.HistoryRemove, syncHistoryRemove);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.SendWatchStates");
        }
    }

    public void GetWatchStates(SVR_AnimeSeries? series = null)
    {
        try
        {
            _logger.LogInformation("Trakt: Getting watch states");

            // check that we have at least one user nominated for Trakt
            var traktEnabledUser = RepoFactory.JMMUser.GetTraktUsers().FirstOrDefault();
            if (traktEnabledUser is null)
            {
                _logger.LogError("Trakt: No trakt users found!");
                return;
            }

            var watchedShows = new Dictionary<int, TraktSyncWatchedShowsResult>();
            var watchedMovies = new Dictionary<int, TraktSyncWatchedMoviesResult>();

            if (!GetWatchedShowsInfo(ref watchedShows) || !GetWatchedMoviesInfo(ref watchedMovies))
                return;

            StoreWatchStates(traktEnabledUser, watchedShows, watchedMovies, series);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.GetWatchStates");
            throw;
        }
    }

    private List<EpisodeSyncDetails> CompareWatchStates(SVR_JMMUser traktEnabledUser, SVR_AnimeSeries series, SVR_AnimeEpisode episode,
        Dictionary<int, TraktSyncWatchedShowsResult> watchedShows, Dictionary<int, TraktSyncWatchedMoviesResult> watchedMovies)
    {
        try
        {
            if (episode.TmdbEpisodeCrossReferences.Count == 0 && episode.TmdbMovieCrossReferences.Count == 0)
            {
                _logger.LogTrace("Trakt: No TMDB links found for {Anime} - {EpisodeType} {Episode}",
                    series.PreferredTitle, episode.EpisodeTypeEnum, episode.AniDB_Episode?.EpisodeNumber);
                return [];
            }

            var episodeSyncDetails = new List<EpisodeSyncDetails>();
            var watchedOnShoko = episode.GetUserRecord(traktEnabledUser.JMMUserID)?.WatchedDate is not null;

            _logger.LogTrace("Trakt: Comparing watch states for {Anime} - {EpisodeType} - {Episode} - Watched: {Watched}",
                series.PreferredTitle, episode.EpisodeTypeEnum, episode.AniDB_Episode?.EpisodeNumber, watchedOnShoko);

            // As we are currently only sending data for watched episodes
            if (!watchedOnShoko)
            {
                _logger.LogTrace("Trakt: Not watched on shoko, not sending watch state");
                return [];
            }

            var tmdbEpisodeIdsToWatch = new List<int>();
            var tmdbMovieIdsToWatch = new List<int>();

            foreach (var tmdbEpisode in episode.TmdbEpisodes)
            {
                watchedShows.TryGetValue(tmdbEpisode.TmdbShowID, out var watchedShow);

                var watchedEpisode = watchedShow?.Seasons
                    .FirstOrDefault(x => x.Number == tmdbEpisode.SeasonNumber)?.Episodes
                    .FirstOrDefault(x => x.Number == tmdbEpisode.EpisodeNumber);

                if (watchedEpisode == null)
                {
                    _logger.LogTrace(
                        "Trakt: Episode not watched on Trakt - TMDB Show: {ShowID} - Season {Season} - Episode {EpNumber}",
                        tmdbEpisode.TmdbShowID, tmdbEpisode.SeasonNumber, tmdbEpisode.EpisodeNumber);
                    tmdbEpisodeIdsToWatch.Add(tmdbEpisode.TmdbEpisodeID);
                }
                else
                {
                    _logger.LogTrace("Trakt: Episode watch state is already in sync");
                }
            }

            foreach (var tmdbMovie in episode.TmdbMovies)
            {
                if (!watchedMovies.ContainsKey(tmdbMovie.TmdbMovieID))
                {
                    _logger.LogTrace(
                        "Trakt: Movie not watched on Trakt - TMDB Movie: {MovieId}", tmdbMovie.TmdbMovieID);
                    tmdbMovieIdsToWatch.Add(tmdbMovie.TmdbMovieID);
                }
                else
                {
                    _logger.LogTrace("Trakt: Movie watch state is already in sync");
                }
            }

            if (tmdbEpisodeIdsToWatch.Count > 0 || tmdbMovieIdsToWatch.Count > 0)
            {
                var episodeDate = GetEpisodeDateForSync(traktEnabledUser, TraktSyncType.HistoryAdd, episode);
                episodeSyncDetails.Add(new EpisodeSyncDetails(TraktSyncType.HistoryAdd, tmdbEpisodeIdsToWatch, tmdbMovieIdsToWatch, episodeDate));
            }

            return episodeSyncDetails;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TraktTVHelper.CompareWatchStates");
            return [];
        }
    }

    private void StoreWatchStates(SVR_JMMUser traktEnabledUser, Dictionary<int, TraktSyncWatchedShowsResult> watchedShows, Dictionary<int, TraktSyncWatchedMoviesResult> watchedMovies, SVR_AnimeSeries? series = null)
    {
        try
        {
            List<SVR_AnimeEpisode> episodes;

            if (series is not null)
            {
                _logger.LogInformation("Trakt: Getting watch states for series {Anime}", series.PreferredTitle);
                episodes = series.AllAnimeEpisodes
                    .Where(x => x.VideoLocals.Count > 0 && x.GetUserRecord(traktEnabledUser.JMMUserID)?.WatchedDate is null)
                    .ToList();
            }
            else
            {
                _logger.LogInformation("Trakt: Getting watch states for whole collection");
                episodes = RepoFactory.AnimeEpisode.GetAll()
                    .Where(x => x.VideoLocals.Count > 0 && x.GetUserRecord(traktEnabledUser.JMMUserID)?.WatchedDate is null)
                    .ToList();
            }

            foreach (var episode in episodes)
            {
                var matchedEpisode = episode.TmdbEpisodes
                    .Select(tmdb => watchedShows.GetValueOrDefault(tmdb.TmdbShowID)
                        ?.Seasons.FirstOrDefault(s => s.Number == tmdb.SeasonNumber)
                        ?.Episodes.FirstOrDefault(e => e.Number == tmdb.EpisodeNumber))
                    .WhereNotNull()
                    .FirstOrDefault();

                if (matchedEpisode is not null)
                {
                    _logger.LogTrace("Trakt: Episode match found for episode ID {EpisodeID}", episode.AnimeEpisodeID);
                    _userDataService.SetEpisodeWatchedStatus(traktEnabledUser, episode, true, matchedEpisode.LastWatchedAt, UserDataSaveReason.TraktSync);
                    // If an episode is watched, we don't need to check movies as well as it's already marked as watched.
                    continue;
                }

                var matchedMovie = episode.TmdbMovies
                    .Select(tmdb => watchedMovies.GetValueOrDefault(tmdb.TmdbMovieID))
                    .WhereNotNull()
                    .FirstOrDefault();

                if (matchedMovie is not null)
                {
                    _logger.LogTrace("Trakt: Movie match found for episode ID {EpisodeID}", episode.AnimeEpisodeID);
                    _userDataService.SetEpisodeWatchedStatus(traktEnabledUser, episode, true, matchedMovie.LastWatchedAt, UserDataSaveReason.TraktSync);
                }
            }
        }
        catch (Exception ex)
        {
            if (series is null)
                _logger.LogError(ex, "Error in TraktTVHelper.StoreWatchStates - Shows");
            else
                _logger.LogError(ex, "Error in TraktTVHelper.StoreWatchStates - Shows for {AnimeTitle}", series.PreferredTitle);
        }
    }

    private static DateTime GetEpisodeDateForSync(SVR_JMMUser traktEnabledUser, TraktSyncType syncType, SVR_AnimeEpisode episode)
    {
        if (syncType is not TraktSyncType.HistoryAdd)
            return DateTime.Now;

        // get the latest user record and find the latest date this episode was watched
        DateTime? thisDate = null;

        var userRecord = episode.GetUserRecord(traktEnabledUser.JMMUserID);
        if (userRecord == null)
        {
            return DateTime.Now;
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

        return thisDate ?? DateTime.Now;
    }

    private static List<int> GetTmdbEpisodeIdsFromEpisode(SVR_AnimeEpisode episode)
    {
        return episode.TmdbEpisodes.Select(x => x.TmdbEpisodeID).ToList();
    }

    private static List<int> GetTmdbMovieIdsFromEpisode(SVR_AnimeEpisode episode)
    {
        return episode.TmdbMovies.Select(x => x.TmdbMovieID).ToList();
    }

    private static TraktSyncHistoryItem CreateHistoryItemFromTmdbId(int tmdbId, DateTime? date = null)
    {
        return new TraktSyncHistoryItem
        {
            IDs = new TraktIds
            {
                TmdbID = tmdbId
            },
            WatchedAt = date
        };
    }
}

internal class EpisodeSyncDetails(TraktSyncType syncType, List<int> tmdbEpisodeIds, List<int> tmdbMovieIds, DateTime? epDate = null)
{
    public TraktSyncType SyncType { get; set; } = syncType;

    public List<int> TmdbEpisodeIds { get; set; } = tmdbEpisodeIds;

    public List<int> TmdbMovieIds { get; set; } = tmdbMovieIds;

    public DateTime? EpDate { get; set; } = epDate;
}
