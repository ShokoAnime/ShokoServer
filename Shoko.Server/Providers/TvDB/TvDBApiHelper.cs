﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Models.TvDB;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Trakt;
using Shoko.Server.Scheduling.Jobs.TvDB;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using TvDbSharper;
using TvDbSharper.Dto;

using SentrySdk = Sentry.SentrySdk;

namespace Shoko.Server.Providers.TvDB;

public class TvDBApiHelper
{
    private readonly ITvDbClient _client;
    private readonly ILogger<TvDBApiHelper> _logger;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IConnectivityService _connectivityService;

    public TvDBApiHelper(ILogger<TvDBApiHelper> logger, ISettingsProvider settingsProvider, IConnectivityService connectivityService, ISchedulerFactory schedulerFactory)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _connectivityService = connectivityService;
        _schedulerFactory = schedulerFactory;
        _client = new TvDbClient();
        _client.BaseUrl = "https://api-beta.thetvdb.com";
    }

    private string CurrentServerTime
    {
        get
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Local);
            var span = DateTime.Now - epoch;
            return ((long)span.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
    }

    private async Task CheckAuthorizationAsync()
    {
        try
        {
            _client.AcceptedLanguage = _settingsProvider.GetSettings().TvDB.Language;
            if (string.IsNullOrEmpty(_client.Authentication.Token))
            {
                TvDBRateLimiter.Instance.EnsureRate();
                await _client.Authentication.AuthenticateAsync(Constants.TvDB.apiKey);
                if (string.IsNullOrEmpty(_client.Authentication.Token))
                {
                    throw new TvDbServerException("Authentication Failed", 200);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in TvDBAuth");
            throw;
        }
    }

    public async Task<TvDB_Series> GetSeriesInfoOnlineAsync(int seriesID, bool forceRefresh)
    {
        try
        {
            var tvSeries = RepoFactory.TvDB_Series.GetByTvDBID(seriesID);
            if (tvSeries != null && !forceRefresh)
            {
                return tvSeries;
            }

            await CheckAuthorizationAsync();

            TvDBRateLimiter.Instance.EnsureRate();
            var response = await _client.Series.GetAsync(seriesID);
            var series = response.Data;

            tvSeries ??= new TvDB_Series();

            tvSeries.PopulateFromSeriesInfo(series);
            RepoFactory.TvDB_Series.Save(tvSeries);

            return tvSeries;
        }
        catch (TvDbServerException exception)
        {
            if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                _client.Authentication.Token = null;
                await CheckAuthorizationAsync();
                if (!string.IsNullOrEmpty(_client.Authentication.Token))
                {
                    return await GetSeriesInfoOnlineAsync(seriesID, forceRefresh);
                }
            }
            // suppress 404 and move on
            else if (exception.StatusCode == (int)HttpStatusCode.NotFound)
            {
                return null;
            }

            _logger.LogError("TvDB returned an error code: {StatusCode}\\n        {Message}",
                exception.StatusCode, exception.Message
            );
            SentrySdk.CaptureException(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TvDBApiHelper.GetSeriesInfoOnline");
            SentrySdk.CaptureException(ex);
        }

        return null;
    }

    public async Task<List<TVDB_Series_Search_Response>> SearchSeriesAsync(string criteria)
    {
        var results = new List<TVDB_Series_Search_Response>();

        try
        {
            await CheckAuthorizationAsync();

            // Search for a series
            _logger.LogTrace("Search TvDB Series: {Criteria}", criteria);

            TvDBRateLimiter.Instance.EnsureRate();
            criteria = criteria.Replace("+", " ");
            var response = await _client.Search.SearchSeriesByNameAsync(criteria);
            var series = response?.Data;
            if (series == null)
            {
                return results;
            }

            foreach (var item in series)
            {
                var searchResult = new TVDB_Series_Search_Response();
                searchResult.Populate(item);
                results.Add(searchResult);
            }
        }
        catch (TvDbServerException exception)
        {
            if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                _client.Authentication.Token = null;
                await CheckAuthorizationAsync();
                if (!string.IsNullOrEmpty(_client.Authentication.Token))
                {
                    return await SearchSeriesAsync(criteria);
                }
                // suppress 404 and move on
            }
            else if (exception.StatusCode == (int)HttpStatusCode.NotFound)
            {
                return results;
            }

            _logger.LogError(exception,
                "TvDB returned an error code: {StatusCode}\\n        {Message}\\n        when searching for {Criteria}",
                exception.StatusCode, exception.Message, criteria
            );
            SentrySdk.CaptureException(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SearchSeries");
            SentrySdk.CaptureException(ex);
        }

        return results;
    }

    public async Task LinkAniDBTvDB(int animeID, int tvDBID, bool additiveLink, bool isAutomatic = false)
    {
        if (!additiveLink)
        {
            // remove all current links
            _logger.LogInformation("Removing All TvDB Links for: {AnimeID}", animeID);
            RemoveAllAniDBTvDBLinks(animeID, false);
        }

        // check if we have this information locally
        // if not download it now
        var tvSeries = RepoFactory.TvDB_Series.GetByTvDBID(tvDBID);
        var scheduler = await _schedulerFactory.GetScheduler();

        if (tvSeries != null || !_connectivityService.NetworkAvailability.HasInternet())
        {
            // download and update series info, episode info and episode images
            // will also download fanart, posters and wide banners
            await scheduler.StartJob<GetTvDBSeriesJob>(c => c.TvDBSeriesID = tvDBID);
        }
        else
        {
            tvSeries = GetSeriesInfoOnlineAsync(tvDBID, true).Result;
        }

        var xref = RepoFactory.CrossRef_AniDB_TvDB.GetByAniDBAndTvDBID(animeID, tvDBID) ??
                   new CrossRef_AniDB_TvDB();

        xref.AniDBID = animeID;

        xref.TvDBID = tvDBID;

        xref.CrossRefSource = isAutomatic ? CrossRefSource.Automatic : CrossRefSource.User;

        RepoFactory.CrossRef_AniDB_TvDB.Save(xref);

        _logger.LogInformation(
            "Adding TvDB Link: AniDB(ID:{AnimeID}) -> TvDB(ID:{TvDbid})", animeID, tvDBID
        );

        var settings = _settingsProvider.GetSettings();
        var series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
        if (settings.TraktTv.Enabled &&
            !string.IsNullOrEmpty(settings.TraktTv.AuthToken) &&
            !series.IsTraktAutoMatchingDisabled)
        {
            // check for Trakt associations
            var trakt = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(animeID);
            if (trakt.Count != 0)
            {
                foreach (var a in trakt)
                {
                    RepoFactory.CrossRef_AniDB_TraktV2.Delete(a);
                }
            }

            await scheduler.StartJob<SearchTraktSeriesJob>(c => c.AnimeID = animeID);
        }
    }

    private void RemoveAllAniDBTvDBLinks(int animeID, bool updateStats = true)
    {
        // check for Trakt associations
        var trakt = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(animeID);
        if (trakt.Count != 0)
        {
            foreach (var a in trakt)
            {
                RepoFactory.CrossRef_AniDB_TraktV2.Delete(a);
            }
        }

        var xrefs = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(animeID);
        if (xrefs == null || xrefs.Count == 0)
        {
            return;
        }

        foreach (var xref in xrefs)
        {
            RepoFactory.CrossRef_AniDB_TvDB.Delete(xref);
        }

        if (updateStats)
        {
            SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);
        }
    }

    public async Task<List<TvDB_Language>> GetLanguagesAsync()
    {
        var languages = new List<TvDB_Language>();

        try
        {
            await CheckAuthorizationAsync();

            TvDBRateLimiter.Instance.EnsureRate();
            var response = await _client.Languages.GetAllAsync();
            var apiLanguages = response.Data;

            if (apiLanguages.Length <= 0)
            {
                return languages;
            }

            foreach (var item in apiLanguages)
            {
                var lan = new TvDB_Language
                {
                    Id = item.Id, EnglishName = item.EnglishName, Name = item.Name, Abbreviation = item.Abbreviation
                };
                languages.Add(lan);
            }
        }
        catch (TvDbServerException exception)
        {
            if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                _client.Authentication.Token = null;
                await CheckAuthorizationAsync();
                if (!string.IsNullOrEmpty(_client.Authentication.Token))
                {
                    return await GetLanguagesAsync();
                }
                // suppress 404 and move on
            }
            else if (exception.StatusCode == (int)HttpStatusCode.NotFound)
            {
                return languages;
            }

            _logger.LogError("TvDB returned an error code: {StatusCode}\\n        {Message}",
                exception.StatusCode, exception.Message
            );
            SentrySdk.CaptureException(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TVDBHelper.GetSeriesBannersOnline");
            SentrySdk.CaptureException(ex);
        }

        return languages;
    }

    public async Task DownloadAutomaticImages(int seriesID, bool forceDownload)
    {
        var summary = await GetSeriesImagesCountsAsync(seriesID);
        if (summary == null) return;

        var settings = _settingsProvider.GetSettings();
        if (summary.Fanart > 0 && settings.TvDB.AutoFanart) await DownloadAutomaticImages(await GetFanartOnlineAsync(seriesID), seriesID, forceDownload);
        if (summary.Poster > 0 && settings.TvDB.AutoPosters) await DownloadAutomaticImages(await GetPosterOnlineAsync(seriesID), seriesID, forceDownload);
        if (summary.Season > 0 && settings.TvDB.AutoWideBanners) await DownloadAutomaticImages(await GetBannerOnlineAsync(seriesID), seriesID, forceDownload);
    }

    private async Task<ImagesSummary> GetSeriesImagesCountsAsync(int seriesID)
    {
        try
        {
            await CheckAuthorizationAsync();

            TvDBRateLimiter.Instance.EnsureRate();
            var response = await _client.Series.GetImagesSummaryAsync(seriesID);
            return response.Data;
        }
        catch (TvDbServerException exception)
        {
            if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                _client.Authentication.Token = null;
                await CheckAuthorizationAsync();
                if (!string.IsNullOrEmpty(_client.Authentication.Token))
                {
                    return await GetSeriesImagesCountsAsync(seriesID);
                }
                // suppress 404 and move on
            }
            else if (exception.StatusCode == (int)HttpStatusCode.NotFound)
            {
                return null;
            }

            _logger.LogError("TvDB returned an error code: {StatusCode}\\n        {Message}",
                exception.StatusCode, exception.Message
            );
            SentrySdk.CaptureException(exception);
        }

        return null;
    }

    private async Task<Image[]> GetSeriesImagesAsync(int seriesID, KeyType type)
    {
        await CheckAuthorizationAsync();

        var query = new ImagesQuery { KeyType = type };
        TvDBRateLimiter.Instance.EnsureRate();
        try
        {
            var response = await _client.Series.GetImagesAsync(seriesID, query);
            return response.Data;
        }
        catch (TvDbServerException exception)
        {
            if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                _client.Authentication.Token = null;
                await CheckAuthorizationAsync();
                if (!string.IsNullOrEmpty(_client.Authentication.Token))
                {
                    return await GetSeriesImagesAsync(seriesID, type);
                }
                // suppress 404 and move on
            }
            else if (exception.StatusCode == (int)HttpStatusCode.NotFound)
            {
                return new Image[] { };
            }

            _logger.LogError("TvDB returned an error code: {StatusCode}\\n        {Message}",
                exception.StatusCode, exception.Message
            );
            SentrySdk.CaptureException(exception);
        }
        catch
        {
            // ignore
        }

        return new Image[] { };
    }

    private async Task<List<TvDB_ImageFanart>> GetFanartOnlineAsync(int seriesID)
    {
        var validIDs = new List<int>();
        var tvImages = new List<TvDB_ImageFanart>();
        try
        {
            var images = await GetSeriesImagesAsync(seriesID, KeyType.Fanart);

            var count = 0;
            var settings = _settingsProvider.GetSettings();
            foreach (var image in images)
            {
                var id = image.Id;
                if (id == 0)
                {
                    continue;
                }

                if (count >= settings.TvDB.AutoFanartAmount)
                {
                    break;
                }

                var img = RepoFactory.TvDB_ImageFanart.GetByTvDBID(id) ?? new TvDB_ImageFanart { Enabled = 1 };

                img.Populate(seriesID, image);
                img.Language = _client.AcceptedLanguage;
                RepoFactory.TvDB_ImageFanart.Save(img);
                tvImages.Add(img);
                validIDs.Add(id);
                count++;
            }

            // delete any images from the database which are no longer valid
            foreach (var img in RepoFactory.TvDB_ImageFanart.GetBySeriesID(seriesID))
            {
                if (!validIDs.Contains(img.Id))
                {
                    RepoFactory.TvDB_ImageFanart.Delete(img);
                }
            }
        }
        catch (TvDbServerException exception)
        {
            if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                _client.Authentication.Token = null;
                await CheckAuthorizationAsync();
                if (!string.IsNullOrEmpty(_client.Authentication.Token))
                {
                    return await GetFanartOnlineAsync(seriesID);
                }
                // suppress 404 and move on
            }
            else if (exception.StatusCode == (int)HttpStatusCode.NotFound)
            {
                return tvImages;
            }

            _logger.LogError("TvDB returned an error code: {StatusCode}\\n        {Message}",
                exception.StatusCode, exception.Message
            );
            SentrySdk.CaptureException(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TVDBApiHelper.GetSeriesFanartOnlineAsync");
            SentrySdk.CaptureException(ex);
        }

        return tvImages;
    }

    private async Task<List<TvDB_ImagePoster>> GetPosterOnlineAsync(int seriesID)
    {
        var validIDs = new List<int>();
        var tvImages = new List<TvDB_ImagePoster>();

        try
        {
            var posters = await GetSeriesImagesAsync(seriesID, KeyType.Poster);
            var season = await GetSeriesImagesAsync(seriesID, KeyType.Season);

            var images = posters.Concat(season).ToArray();

            var count = 0;
            var settings = _settingsProvider.GetSettings();
            foreach (var image in images)
            {
                var id = image.Id;
                if (id == 0)
                {
                    continue;
                }

                if (count >= settings.TvDB.AutoPostersAmount)
                {
                    break;
                }

                var img = RepoFactory.TvDB_ImagePoster.GetByTvDBID(id) ?? new TvDB_ImagePoster { Enabled = 1 };

                img.Populate(seriesID, image);
                img.Language = _client.AcceptedLanguage;
                RepoFactory.TvDB_ImagePoster.Save(img);
                validIDs.Add(id);
                tvImages.Add(img);
                count++;
            }

            // delete any images from the database which are no longer valid
            foreach (var img in RepoFactory.TvDB_ImagePoster.GetBySeriesID(seriesID))
            {
                if (!validIDs.Contains(img.Id))
                {
                    RepoFactory.TvDB_ImagePoster.Delete(img.TvDB_ImagePosterID);
                }
            }
        }
        catch (TvDbServerException exception)
        {
            if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                _client.Authentication.Token = null;
                await CheckAuthorizationAsync();
                if (!string.IsNullOrEmpty(_client.Authentication.Token))
                {
                    return await GetPosterOnlineAsync(seriesID);
                }
                // suppress 404 and move on
            }
            else if (exception.StatusCode == (int)HttpStatusCode.NotFound)
            {
                return tvImages;
            }

            _logger.LogError("TvDB returned an error code: {StatusCode}\\n        {Message}",
                exception.StatusCode, exception.Message
            );
            SentrySdk.CaptureException(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TVDBApiHelper.GetPosterOnlineAsync");
            SentrySdk.CaptureException(ex);
        }

        return tvImages;
    }

    private async Task<List<TvDB_ImageWideBanner>> GetBannerOnlineAsync(int seriesID)
    {
        var validIDs = new List<int>();
        var tvImages = new List<TvDB_ImageWideBanner>();

        try
        {
            var season = await GetSeriesImagesAsync(seriesID, KeyType.Seasonwide);
            var series = await GetSeriesImagesAsync(seriesID, KeyType.Series);

            var images = season.Concat(series).ToArray();

            var count = 0;
            var settings = _settingsProvider.GetSettings();
            foreach (var image in images)
            {
                var id = image.Id;
                if (id == 0)
                {
                    continue;
                }

                if (count >= settings.TvDB.AutoWideBannersAmount)
                {
                    break;
                }

                var img = RepoFactory.TvDB_ImageWideBanner.GetByTvDBID(id) ?? new TvDB_ImageWideBanner { Enabled = 1 };

                img.Populate(seriesID, image);
                img.Language = _client.AcceptedLanguage;
                RepoFactory.TvDB_ImageWideBanner.Save(img);
                validIDs.Add(id);
                tvImages.Add(img);
                count++;
            }

            // delete any images from the database which are no longer valid
            foreach (var img in RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(seriesID))
            {
                if (!validIDs.Contains(img.Id))
                {
                    RepoFactory.TvDB_ImageWideBanner.Delete(img.TvDB_ImageWideBannerID);
                }
            }
        }
        catch (TvDbServerException exception)
        {
            if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                _client.Authentication.Token = null;
                await CheckAuthorizationAsync();
                if (!string.IsNullOrEmpty(_client.Authentication.Token))
                {
                    return await GetBannerOnlineAsync(seriesID);
                }
                // suppress 404 and move on
            }
            else if (exception.StatusCode == (int)HttpStatusCode.NotFound)
            {
                return tvImages;
            }

            _logger.LogError("TvDB returned an error code: {StatusCode}\\n        {Message}",
                exception.StatusCode, exception.Message
            );
            SentrySdk.CaptureException(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TVDBApiHelper.GetPosterOnlineAsync");
            SentrySdk.CaptureException(ex);
        }

        return tvImages;
    }

    private async Task DownloadAutomaticImages(List<TvDB_ImageFanart> images, int seriesID, bool forceDownload)
    {
        var settings = _settingsProvider.GetSettings();
        var scheduler = await _schedulerFactory.GetScheduler();
        if (!settings.TvDB.AutoFanart) return;
        // find out how many images we already have locally
        var imageCount = RepoFactory.TvDB_ImageFanart.GetBySeriesID(seriesID).Count(fanart =>
            !string.IsNullOrEmpty(fanart.GetFullImagePath()) && File.Exists(fanart.GetFullImagePath()));

        foreach (var img in images)
        {
            if (imageCount < settings.TvDB.AutoFanartAmount && !string.IsNullOrEmpty(img.GetFullImagePath()))
            {
                var fileExists = File.Exists(img.GetFullImagePath());
                if (fileExists && !forceDownload) continue;

                await scheduler.StartJob<DownloadTvDBImageJob>(c =>
                    {
                        c.ImageID = img.TvDB_ImageFanartID;
                        c.ImageType = ImageEntityType.TvDB_FanArt;
                        c.ForceDownload = forceDownload;
                    }
                );
                imageCount++;
            }
            else
            {
                //The TvDB_AutoFanartAmount point to download less images than its available
                // we should clean those image that we didn't download because those dont exists in local repo
                // first we check if file was downloaded
                if (string.IsNullOrEmpty(img.GetFullImagePath()) || !File.Exists(img.GetFullImagePath()))
                {
                    RepoFactory.TvDB_ImageFanart.Delete(img.TvDB_ImageFanartID);
                }
            }
        }
    }

    private async Task DownloadAutomaticImages(List<TvDB_ImagePoster> images, int seriesID, bool forceDownload)
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TvDB.AutoPosters) return;
        // find out how many images we already have locally
        var imageCount = RepoFactory.TvDB_ImagePoster.GetBySeriesID(seriesID).Count(fanart =>
            !string.IsNullOrEmpty(fanart.GetFullImagePath()) && File.Exists(fanart.GetFullImagePath()));

        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var img in images)
        {
            if (imageCount < settings.TvDB.AutoPostersAmount && !string.IsNullOrEmpty(img.GetFullImagePath()))
            {
                var fileExists = File.Exists(img.GetFullImagePath());
                if (fileExists && !forceDownload) continue;

                await scheduler.StartJob<DownloadTvDBImageJob>(c =>
                    {
                        c.ImageID = img.TvDB_ImagePosterID;
                        c.ImageType = ImageEntityType.TvDB_Cover;
                        c.ForceDownload = forceDownload;
                    }
                );
                imageCount++;
            }
            else
            {
                //The TvDB_AutoFanartAmount point to download less images than its available
                // we should clean those image that we didn't download because those dont exists in local repo
                // first we check if file was downloaded
                if (string.IsNullOrEmpty(img.GetFullImagePath()) || !File.Exists(img.GetFullImagePath()))
                {
                    RepoFactory.TvDB_ImagePoster.Delete(img);
                }
            }
        }
    }

    private async Task DownloadAutomaticImages(List<TvDB_ImageWideBanner> images, int seriesID, bool forceDownload)
    {
        var settings = _settingsProvider.GetSettings();
        // find out how many images we already have locally
        if (!settings.TvDB.AutoWideBanners) return;
        var imageCount = RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(seriesID).Count(banner =>
            !string.IsNullOrEmpty(banner.GetFullImagePath()) && File.Exists(banner.GetFullImagePath()));

        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var img in images)
        {
            if (imageCount < settings.TvDB.AutoWideBannersAmount && !string.IsNullOrEmpty(img.GetFullImagePath()))
            {
                var fileExists = File.Exists(img.GetFullImagePath());
                if (fileExists && !forceDownload) continue;
                await scheduler.StartJob<DownloadTvDBImageJob>(c =>
                    {
                        c.ImageID = img.TvDB_ImageWideBannerID;
                        c.ImageType = ImageEntityType.TvDB_Banner;
                        c.ForceDownload = forceDownload;
                    }
                );
                imageCount++;
            }
            else
            {
                // The TvDB_AutoFanartAmount point to download less images than its available
                // we should clean those image that we didn't download because those dont exists in local repo
                // first we check if file was downloaded
                if (string.IsNullOrEmpty(img.GetFullImagePath()) || !File.Exists(img.GetFullImagePath()))
                    RepoFactory.TvDB_ImageWideBanner.Delete(img);
            }
        }
    }

    private async Task<List<EpisodeRecord>> GetEpisodesOnlineAsync(int seriesID)
    {
        var apiEpisodes = new List<EpisodeRecord>();
        try
        {
            await CheckAuthorizationAsync();

            var tasks = new List<Task<TvDbResponse<EpisodeRecord[]>>>();
            TvDBRateLimiter.Instance.EnsureRate();
            var firstResponse = await _client.Series.GetEpisodesAsync(seriesID, 1);
            _logger.LogTrace(
                "First Page: First: {First} Next: {Next} Previous: {Previous} Last: {Last}",
                firstResponse?.Links?.First?.ToString() ?? "NULL", firstResponse?.Links?.Next?.ToString() ?? "NULL",
                firstResponse?.Links?.Prev?.ToString() ?? "NULL", firstResponse?.Links?.Last?.ToString() ?? "NULL"
            );

            for (var i = 2; i <= (firstResponse?.Links?.Last ?? 1); i++)
            {
                _logger.LogTrace("Adding Task: {I}", i);
                TvDBRateLimiter.Instance.EnsureRate();
                tasks.Add(_client.Series.GetEpisodesAsync(seriesID, i));
            }

            var results = await Task.WhenAll(tasks);
            var lastresponse = results.Length == 0 ? firstResponse : results.Last();
            _logger.LogTrace("Last Page: First: {First} Next: {Next} Previous: {Previous} Last: {Last}",
                lastresponse?.Links?.First?.ToString() ?? "NULL", lastresponse?.Links?.Next?.ToString() ?? "NULL",
                lastresponse?.Links?.Prev?.ToString() ?? "NULL", lastresponse?.Links?.Last?.ToString() ?? "NULL");
            _logger.LogTrace("Last Count: {Last}", lastresponse?.Data.Length.ToString() ?? "NULL");
            apiEpisodes = firstResponse?.Data?.Concat(results.SelectMany(x => x.Data)).ToList();
        }
        catch (TvDbServerException exception)
        {
            if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                _client.Authentication.Token = null;
                await CheckAuthorizationAsync();
                if (!string.IsNullOrEmpty(_client.Authentication.Token))
                {
                    return await GetEpisodesOnlineAsync(seriesID);
                }
            }
            // suppress 404 and move on
            else if (exception.StatusCode == (int)HttpStatusCode.NotFound)
            {
                return apiEpisodes;
            }

            _logger.LogError("TvDB returned an error code: {StatusCode}\\n        {Message}",
                exception.StatusCode, exception.Message
            );
            SentrySdk.CaptureException(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TvDBApiHelper.GetEpisodesOnlineAsync");
            SentrySdk.CaptureException(ex);
        }

        return apiEpisodes;
    }

    public async Task<TvDB_Series> UpdateSeriesInfoAndImages(int seriesID, bool forceRefresh, bool downloadImages)
    {
        try
        {
            // update the series info
            var scheduler = await _schedulerFactory.GetScheduler();
            var tvSeries = await GetSeriesInfoOnlineAsync(seriesID, forceRefresh);
            if (tvSeries == null)
                return null;

            if (downloadImages)
            {
                await DownloadAutomaticImages(seriesID, forceRefresh);
            }

            var episodeItems = await GetEpisodesOnlineAsync(seriesID);
            _logger.LogTrace("Found {Count} Episode nodes", episodeItems.Count);

            var existingEpIds = new List<int>();
            foreach (var item in episodeItems)
            {
                if (!existingEpIds.Contains(item.Id))
                {
                    existingEpIds.Add(item.Id);
                }

                var ep = RepoFactory.TvDB_Episode.GetByTvDBID(item.Id) ?? new TvDB_Episode();
                ep.Populate(item);
                RepoFactory.TvDB_Episode.Save(ep);

                if (!downloadImages)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(ep.Filename))
                {
                    continue;
                }

                var fileExists = File.Exists(ep.GetFullImagePath());
                if (fileExists && !forceRefresh)
                {
                    continue;
                }

                await scheduler.StartJob<DownloadTvDBImageJob>(c =>
                    {
                        c.ImageID = ep.TvDB_EpisodeID;
                        c.ImageType = ImageEntityType.TvDB_Episode;
                        c.ForceDownload = forceRefresh;
                    }
                );
            }

            // get all the existing tvdb episodes, to see if any have been deleted
            var allEps = RepoFactory.TvDB_Episode.GetBySeriesID(seriesID);
            foreach (var oldEp in allEps)
            {
                if (!existingEpIds.Contains(oldEp.Id))
                {
                    RepoFactory.TvDB_Episode.Delete(oldEp.TvDB_EpisodeID);
                }
            }

            // Updating stats as it will not happen with the episodes
            RepoFactory.CrossRef_AniDB_TvDB.GetByTvDBID(seriesID).Select(a => a.AniDBID).Distinct()
                .ForEach(SVR_AniDB_Anime.UpdateStatsByAnimeID);

            return tvSeries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TVDBHelper.GetEpisodes");
            SentrySdk.CaptureException(ex);
            return null;
        }
    }

    public void LinkAniDBTvDBEpisode(int aniDBID, int tvDBID)
    {
        var xref =
            RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAniDBAndTvDBEpisodeIDs(aniDBID, tvDBID) ??
            new CrossRef_AniDB_TvDB_Episode_Override();

        xref.AniDBEpisodeID = aniDBID;
        xref.TvDBEpisodeID = tvDBID;
        RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.Save(xref);

        var ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(aniDBID);

        SVR_AniDB_Anime.UpdateStatsByAnimeID(ep.AniDB_Episode.AnimeID);
        RepoFactory.AnimeEpisode.Save(ep);

        _logger.LogTrace("Changed tvdb episode association: {AniDbid}", aniDBID);
    }

    // Removes all TVDB information from a series, bringing it back to a blank state.
    public void RemoveLinkAniDBTvDB(int animeID, int tvDBID)
    {
        var xref = RepoFactory.CrossRef_AniDB_TvDB.GetByAniDBAndTvDBID(animeID, tvDBID);
        if (xref == null)
        {
            return;
        }

        RepoFactory.CrossRef_AniDB_TvDB.Delete(xref);
        RepoFactory.CrossRef_AniDB_TvDB_Episode.Delete(
            RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAnimeID(animeID));
        RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.Delete(
            RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAnimeID(animeID));

        // Disable auto-matching when we remove an existing match for the series.
        var series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
        if (series != null)
        {
            series.IsTvDBAutoMatchingDisabled = true;
            RepoFactory.AnimeSeries.Save(series, false, true, true);
        }

        SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);
    }

    public async Task ScanForMatches()
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TvDB.AutoLink)
            return;

        IReadOnlyList<SVR_AnimeSeries> allSeries = RepoFactory.CrossRef_AniDB_TvDB.GetSeriesWithoutLinks();

        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var ser in allSeries)
        {
            if (ser.IsTvDBAutoMatchingDisabled)
                continue;

            var anime = ser.GetAnime();
            if (anime == null)
                continue;

            _logger.LogTrace("Found anime without TvDB association: {MaintTitle}", anime.MainTitle);
            await scheduler.StartJob<SearchTvDBSeriesJob>(c => c.AnimeID = ser.AniDB_ID);
        }
    }

    public async Task UpdateAllInfo(bool force)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var allCrossRefs = RepoFactory.CrossRef_AniDB_TvDB.GetAll();
        foreach (var xref in allCrossRefs)
        {
            await scheduler.StartJob<GetTvDBSeriesJob>(c =>
                {
                    c.TvDBSeriesID = xref.TvDBID;
                    c.ForceRefresh = force;
                }
            );
        }
    }

    private List<int> GetUpdatedSeriesList(long serverTime)
    {
        return GetUpdatedSeriesListAsync(serverTime).Result;
    }

    private async Task<List<int>> GetUpdatedSeriesListAsync(long lasttimeseconds)
    {
        var seriesList = new List<int>();
        try
        {
            // Unix timestamp is seconds past epoch
            var lastUpdateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            lastUpdateTime = lastUpdateTime.AddSeconds(lasttimeseconds).ToLocalTime();

            // api limits this to a week at a time, so split it up
            var spans = new List<(DateTime, DateTime)>();
            if (lastUpdateTime.AddDays(7) < DateTime.Now)
            {
                var time = lastUpdateTime;
                while (time < DateTime.Now)
                {
                    var nextTime = time.AddDays(7);
                    if (nextTime > DateTime.Now)
                    {
                        nextTime = DateTime.Now;
                    }

                    spans.Add((time, nextTime));
                    time = time.AddDays(7);
                }
            }
            else
            {
                spans.Add((lastUpdateTime, DateTime.Now));
            }

            var i = 1;
            var count = spans.Count;
            foreach (var span in spans)
            {
                TvDBRateLimiter.Instance.EnsureRate();
                // this may take a while if you don't keep shoko running, so log info
                _logger.LogInformation("Getting updates from TvDB, part {I} of {Count}", i, count);
                i++;
                var response = await _client.Updates.GetAsync(span.Item1, span.Item2);

                var updates = response?.Data;
                if (updates == null)
                {
                    continue;
                }

                seriesList.AddRange(updates.Where(item => item != null).Select(item => item.Id));
            }

            return seriesList;
        }
        catch (TvDbServerException exception)
        {
            switch (exception.StatusCode)
            {
                case (int)HttpStatusCode.Unauthorized:
                    {
                        _client.Authentication.Token = null;
                        await CheckAuthorizationAsync();
                        if (!string.IsNullOrEmpty(_client.Authentication.Token))
                        {
                            return await GetUpdatedSeriesListAsync(lasttimeseconds);
                        }

                        // suppress 404 and move on
                        break;
                    }
                case (int)HttpStatusCode.NotFound: return seriesList;
            }

            _logger.LogError("TvDB returned an error code: {StatusCode}\\n        {Message}",
                exception.StatusCode, exception.Message
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetUpdatedSeriesList");
        }

        return seriesList;
    }

    // ReSharper disable once RedundantAssignment
    public string IncrementalTvDBUpdate(ref List<int> tvDBIDs, ref bool tvDBOnline)
    {
        // check if we have record of doing an automated update for the TvDB previously
        // if we have then we have kept a record of the server time and can do a delta update
        // otherwise we need to do a full update and keep a record of the time

        var allTvDBIDs = new List<int>();
        tvDBIDs ??= new List<int>();
        tvDBOnline = true;

        try
        {
            // record the tvdb server time when we started
            // we record the time now instead of after we finish, to include any possible misses
            var currentTvDBServerTime = CurrentServerTime;
            if (currentTvDBServerTime.Length == 0)
            {
                tvDBOnline = false;
                return currentTvDBServerTime;
            }

            foreach (var ser in RepoFactory.AnimeSeries.GetAll())
            {
                var xrefs = ser.GetCrossRefTvDB();
                if (xrefs == null)
                {
                    continue;
                }

                foreach (var xref in xrefs)
                {
                    if (!allTvDBIDs.Contains(xref.TvDBID))
                    {
                        allTvDBIDs.Add(xref.TvDBID);
                    }
                }
            }

            // get the time we last did a TvDB update
            // if this is the first time it will be null
            // update the anidb info ever 24 hours

            var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TvDBInfo);

            var lastServerTime = string.Empty;
            if (sched != null)
            {
                var ts = DateTime.Now - sched.LastUpdate;
                _logger.LogTrace("Last tvdb info update was {TotalHours} hours ago", ts.TotalHours);
                if (!string.IsNullOrEmpty(sched.UpdateDetails))
                {
                    lastServerTime = sched.UpdateDetails;
                }

                // the UpdateDetails field for this type will actually contain the last server time from
                // TheTvDB that a full update was performed
            }


            // get a list of updates from TvDB since that time
            if (lastServerTime.Length > 0)
            {
                if (!long.TryParse(lastServerTime, out var lasttimeseconds))
                {
                    lasttimeseconds = -1;
                }

                if (lasttimeseconds < 0)
                {
                    tvDBIDs = allTvDBIDs;
                    return CurrentServerTime;
                }

                var seriesList = GetUpdatedSeriesList(lasttimeseconds);
                _logger.LogTrace("{Count} series have been updated since last download", seriesList.Count);
                _logger.LogTrace("{Count} TvDB series locally", allTvDBIDs.Count);

                foreach (var id in seriesList)
                {
                    if (allTvDBIDs.Contains(id))
                    {
                        tvDBIDs.Add(id);
                    }
                }

                _logger.LogTrace("{Count} TvDB local series have been updated since last download", tvDBIDs.Count);
            }
            else
            {
                // use the full list
                tvDBIDs = allTvDBIDs;
            }

            return CurrentServerTime;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IncrementalTvDBUpdate");
            return string.Empty;
        }
    }
}
