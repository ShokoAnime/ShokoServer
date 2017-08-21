using Shoko.Models.Server;
using TvDbSharper;
using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using Shoko.Server.Repositories;
using Shoko.Server.Extensions;
using Shoko.Models.TvDB;
using Shoko.Models.Enums;
using Shoko.Server.Models;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;
using Pri.LongPath;

using Shoko.Commons.Extensions;
using TvDbSharper.Dto;
using HttpUtility = Nancy.Helpers.HttpUtility;

namespace Shoko.Server.Providers.TvDB
{
    public class TvDBApiHelper
    {
        static ITvDbClient client = new TvDbClient();
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static string CurrentServerTime
        {
            get
            {
                DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime();
                TimeSpan span = (new DateTime().ToLocalTime() - epoch);
                return span.TotalSeconds.ToString(CultureInfo.InvariantCulture);
            }
        }

        public TvDBApiHelper()
        {
        }

        private static async Task CheckAuthorizationAsync()
        {
            try
            {
                client.AcceptedLanguage = ServerSettings.TvDB_Language;
                if (string.IsNullOrEmpty(client.Authentication.Token))
                {
                    TvDBRateLimiter.Instance.EnsureRate();
                    await client.Authentication.AuthenticateAsync(Constants.TvDB.apiKey);
                    if (string.IsNullOrEmpty(client.Authentication.Token))
                        throw new TvDbServerException("Authentication Failed", 200);
                }
            }
            catch (Exception e)
            {
                logger.Error(e, $"Error in TvDBAuth: {e}");
                throw;
            }
        }

        public static TvDB_Series GetSeriesInfoOnline(int seriesID)
        {
            return Task.Run(async () => await GetSeriesInfoOnlineAsync(seriesID)).Result;
        }

        public static async Task<TvDB_Series> GetSeriesInfoOnlineAsync(int seriesID)
        {
            try
            {
                await CheckAuthorizationAsync();

                TvDBRateLimiter.Instance.EnsureRate();
                var response = await client.Series.GetAsync(seriesID);
                Series series = response.Data;

                TvDB_Series tvSeries = RepoFactory.TvDB_Series.GetByTvDBID(seriesID);
                if (tvSeries == null)
                    tvSeries = new TvDB_Series();

                tvSeries.PopulateFromSeriesInfo(series);
                RepoFactory.TvDB_Series.Save(tvSeries);

                return tvSeries;
            }
            catch (TvDbSharper.TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetSeriesInfoOnlineAsync(seriesID);
                    // suppress 404 and move on
                } else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return null;

                logger.Error(exception, "TvDB returned an error code: " + exception.StatusCode + "\n        " + exception.Message);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TvDBApiHelper.GetSeriesInfoOnline: " + ex.ToString());
            }

            return null;
        }

        public static List<TVDB_Series_Search_Response> SearchSeries(string criteria)
        {
            return Task.Run(async () => await SearchSeriesAsync(criteria)).Result;
        }

        public static async Task<List<TVDB_Series_Search_Response>> SearchSeriesAsync(string criteria)
        {
            List<TVDB_Series_Search_Response> results = new List<TVDB_Series_Search_Response>();

            try
            {
                await CheckAuthorizationAsync();

                // Search for a series
                logger.Trace("Search TvDB Series: {0}", criteria);

                TvDBRateLimiter.Instance.EnsureRate();
                var response = await client.Search.SearchSeriesByNameAsync(criteria);
                TvDbSharper.Dto.SeriesSearchResult[] series = response.Data;

                foreach (TvDbSharper.Dto.SeriesSearchResult item in series)
                {
                    TVDB_Series_Search_Response searchResult = new TVDB_Series_Search_Response();
                    searchResult.Populate(item);
                    results.Add(searchResult);
                }
            }
            catch (TvDbSharper.TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await SearchSeriesAsync(criteria);
                    // suppress 404 and move on
                } else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return results;
                logger.Error(exception, "TvDB returned an error code: " + exception.StatusCode + "\n        " + exception.Message + "\n        when searching for " + criteria);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in SearchSeries: " + ex.ToString());
            }

            return results;
        }

        public static string LinkAniDBTvDB(int animeID, EpisodeType aniEpType, int aniEpNumber, int tvDBID,
            int tvSeasonNumber, int tvEpNumber, bool excludeFromWebCache, bool additiveLink = false)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                if (!additiveLink)
                    // remove all current links
                    RemoveAllAniDBTvDBLinks(animeID, -1, false);

                // check if we have this information locally
                // if not download it now
                TvDB_Series tvSeries = RepoFactory.TvDB_Series.GetByTvDBID(tvDBID);
                if (tvSeries == null)
                {
                    // we download the series info here just so that we have the basic info in the
                    // database before the queued task runs later
                    tvSeries = GetSeriesInfoOnline(tvDBID);
                }

                // download and update series info, episode info and episode images
                // will also download fanart, posters and wide banners
                CommandRequest_TvDBUpdateSeriesAndEpisodes cmdSeriesEps =
                    new CommandRequest_TvDBUpdateSeriesAndEpisodes(tvDBID,
                        false);
                //Optimize for batch updates, if there are a lot of LinkAniDBTvDB commands queued 
                //this will cause only one updateSeriesAndEpisodes command to be created
                if (RepoFactory.CommandRequest.GetByCommandID(cmdSeriesEps.CommandID) == null)
                {
                    cmdSeriesEps.Save();
                }

                CrossRef_AniDB_TvDBV2 xref = RepoFactory.CrossRef_AniDB_TvDBV2.GetByTvDBID(tvDBID, tvSeasonNumber,
                    tvEpNumber, animeID, (int) aniEpType, aniEpNumber);
                if (xref == null)
                    xref = new CrossRef_AniDB_TvDBV2();

                xref.AnimeID = animeID;
                xref.AniDBStartEpisodeType = (int)aniEpType;
                xref.AniDBStartEpisodeNumber = aniEpNumber;

                xref.TvDBID = tvDBID;
                xref.TvDBSeasonNumber = tvSeasonNumber;
                xref.TvDBStartEpisodeNumber = tvEpNumber;
                if (tvSeries != null)
                    xref.TvDBTitle = tvSeries.SeriesName;

                if (excludeFromWebCache)
                    xref.CrossRefSource = (int)CrossRefSource.WebCache;
                else
                    xref.CrossRefSource = (int)CrossRefSource.User;

                RepoFactory.CrossRef_AniDB_TvDBV2.Save(xref);

                logger.Trace("Changed tvdb association: {0}", animeID);

                if (!excludeFromWebCache)
                {
                    var req = new CommandRequest_WebCacheSendXRefAniDBTvDB(xref.CrossRef_AniDB_TvDBV2ID);
                    req.Save();
                }

                if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                {
                    // check for Trakt associations
                    List<CrossRef_AniDB_TraktV2> trakt = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(animeID);
                    if (trakt.Count != 0)
                    {
                        // remove them and rescan
                        foreach (CrossRef_AniDB_TraktV2 a in trakt)
                        {
                            RepoFactory.CrossRef_AniDB_TraktV2.Delete(a);
                        }
                    }

                    var cmd2 = new CommandRequest_TraktSearchAnime(animeID, false);
                    cmd2.Save(session);
                }
            }

            return "";
        }

        public static void RemoveAllAniDBTvDBLinks(int animeID, int aniEpType = -1, bool updateStats = true)
        {
            // check for Trakt associations
            List<CrossRef_AniDB_TraktV2> trakt = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(animeID);
            if (trakt.Count != 0)
            {
                // remove them and rescan
                foreach (CrossRef_AniDB_TraktV2 a in trakt)
                {
                    RepoFactory.CrossRef_AniDB_TraktV2.Delete(a);
                }
            }

            List<CrossRef_AniDB_TvDBV2> xrefs = RepoFactory.CrossRef_AniDB_TvDBV2.GetByAnimeID(animeID);
            if (xrefs == null || xrefs.Count == 0) return;

            foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
            {
                if (aniEpType != -1 && aniEpType == xref.AniDBStartEpisodeType) continue;

                RepoFactory.CrossRef_AniDB_TvDBV2.Delete(xref.CrossRef_AniDB_TvDBV2ID);

                if (aniEpType == -1)
                {
                    foreach (EpisodeType eptype in Enum.GetValues(typeof(EpisodeType)))
                    {
                        CommandRequest_WebCacheDeleteXRefAniDBTvDB req = new CommandRequest_WebCacheDeleteXRefAniDBTvDB(
                            animeID,
                            (int)eptype, xref.AniDBStartEpisodeNumber,
                            xref.TvDBID, xref.TvDBSeasonNumber, xref.TvDBStartEpisodeNumber);
                        req.Save();
                    }
                }
                else
                {
                    CommandRequest_WebCacheDeleteXRefAniDBTvDB req = new CommandRequest_WebCacheDeleteXRefAniDBTvDB(
                        animeID,
                        aniEpType, xref.AniDBStartEpisodeNumber,
                        xref.TvDBID, xref.TvDBSeasonNumber, xref.TvDBStartEpisodeNumber);
                    req.Save();
                }
            }

            if (updateStats) SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);
        }

        public static List<TvDB_Language> GetLanguages()
        {
            return Task.Run(async () => await GetLanguagesAsync()).Result;
        }

        public static async Task<List<TvDB_Language>> GetLanguagesAsync()
        {
            List<TvDB_Language> languages = new List<TvDB_Language>();

            try
            {
                await CheckAuthorizationAsync();

                TvDBRateLimiter.Instance.EnsureRate();
                var response = await client.Languages.GetAllAsync();
                TvDbSharper.Dto.Language[] apiLanguages = response.Data;

                if (apiLanguages.Length <= 0)
                    return languages;

                foreach (TvDbSharper.Dto.Language item in apiLanguages)
                {
                    TvDB_Language lan = new TvDB_Language()
                    {
                        Id = item.Id,
                        EnglishName = item.EnglishName,
                        Name = item.Name,
                        Abbreviation = item.Abbreviation
                    };
                    languages.Add(lan);
                }
            }
            catch (TvDbSharper.TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetLanguagesAsync();
                    // suppress 404 and move on
                } else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return languages;
                logger.Error(exception, "TvDB returned an error code: " + exception.StatusCode + "\n        " + exception.Message);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TVDBHelper.GetSeriesBannersOnline: " + ex.ToString());
            }

            return languages;
        }

        public static void DownloadAutomaticImages(int seriesID, bool forceDownload)
        {
            ImagesSummary summary = GetSeriesImagesCounts(seriesID);
            if (summary.Fanart > 0)
            {
                DownloadAutomaticImages(GetFanartOnline(seriesID), seriesID, forceDownload);
            }
            if (summary.Poster > 0 || summary.Season > 0)
            {
                DownloadAutomaticImages(GetPosterOnline(seriesID), seriesID, forceDownload);
            }
            if (summary.Seasonwide > 0 || summary.Series > 0)
            {
                DownloadAutomaticImages(GetBannerOnline(seriesID), seriesID, forceDownload);
            }
        }

        static ImagesSummary GetSeriesImagesCounts(int seriesID)
        {
            return Task.Run(async () => await GetSeriesImagesCountsAsync(seriesID)).Result;
        }

        static async Task<ImagesSummary> GetSeriesImagesCountsAsync(int seriesID)
        {
            try
            {
                await CheckAuthorizationAsync();

                TvDBRateLimiter.Instance.EnsureRate();
                var response = await client.Series.GetImagesSummaryAsync(seriesID);
                return response.Data;
            }
            catch (TvDbSharper.TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetSeriesImagesCountsAsync(seriesID);
                    // suppress 404 and move on
                } else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return null;
                logger.Error(exception, "TvDB returned an error code: " + exception.StatusCode + "\n        " + exception.Message);
            }
            return null;
        }

        static async Task<Image[]> GetSeriesImagesAsync(int seriesID, KeyType type)
        {
            await CheckAuthorizationAsync();

            ImagesQuery query = new ImagesQuery()
            {
                KeyType = type
            };
            TvDBRateLimiter.Instance.EnsureRate();
            try
            {
                var response = await client.Series.GetImagesAsync(seriesID, query);
                return response.Data;
            }
            catch (TvDbSharper.TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetSeriesImagesAsync(seriesID, type);
                    // suppress 404 and move on
                }
                else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return new Image[] { };
                logger.Error(exception,
                    "TvDB returned an error code: " + exception.StatusCode + "\n        " + exception.Message);
            }
            catch
            {
            }
            return new Image[] { };
        }

        public static List<TvDB_ImageFanart> GetFanartOnline(int seriesID)
        {
            return Task.Run(async () => await GetFanartOnlineAsync(seriesID)).Result;
        }

        public static async Task<List<TvDB_ImageFanart>> GetFanartOnlineAsync(int seriesID)
        {
            List<int> validIDs = new List<int>();
            List<TvDB_ImageFanart> tvImages = new List<TvDB_ImageFanart>();
            try
            {
                Image[] images = await GetSeriesImagesAsync(seriesID, KeyType.Fanart);

                foreach (Image image in images)
                {
                    int id = image.Id ?? 0;
                    if (id == 0) { continue; }

                    TvDB_ImageFanart img = RepoFactory.TvDB_ImageFanart.GetByTvDBID(id);
                    
                    if (img == null)
                    {
                        img = new TvDB_ImageFanart()
                        {
                            Enabled = 1
                        };
                    }

                    img.Populate(seriesID, image);
                    img.Language = client.AcceptedLanguage;
                    RepoFactory.TvDB_ImageFanart.Save(img);
                    tvImages.Add(img);
                    validIDs.Add(id);
                }

                // delete any images from the database which are no longer valid
                foreach (TvDB_ImageFanart img in RepoFactory.TvDB_ImageFanart.GetBySeriesID(seriesID))
                {
                    if (!validIDs.Contains(img.Id))
                        RepoFactory.TvDB_ImageFanart.Delete(img.TvDB_ImageFanartID);
                }
            }
            catch (TvDbSharper.TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetFanartOnlineAsync(seriesID);
                    // suppress 404 and move on
                }
                else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return tvImages;
                logger.Error(exception, "TvDB returned an error code: " + exception.StatusCode + "\n        " + exception.Message);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TVDBApiHelper.GetSeriesBannersOnlineAsync: " + ex.ToString());
            }

            return tvImages;
        }

        public static List<TvDB_ImagePoster> GetPosterOnline(int seriesID)
        {
            return Task.Run(async () => await GetPosterOnlineAsync(seriesID)).Result;
        }

        public static async Task<List<TvDB_ImagePoster>> GetPosterOnlineAsync(int seriesID)
        {
            List<int> validIDs = new List<int>();
            List<TvDB_ImagePoster> tvImages = new List<TvDB_ImagePoster>();

            try
            {
                Image[] posters = await GetSeriesImagesAsync(seriesID, KeyType.Poster);
                Image[] season = await GetSeriesImagesAsync(seriesID, KeyType.Season);

                Image[] images = posters.Concat(season).ToArray();

                foreach (Image image in images)
                {
                    int id = image.Id ?? 0;
                    if (id == 0) { continue; }

                    TvDB_ImagePoster img = RepoFactory.TvDB_ImagePoster.GetByTvDBID(id);

                    if (img == null)
                    {
                        img = new TvDB_ImagePoster()
                        {
                            Enabled = 1
                        };
                    }

                    img.Populate(seriesID, image);
                    img.Language = client.AcceptedLanguage;
                    RepoFactory.TvDB_ImagePoster.Save(img);
                    validIDs.Add(id);
                    tvImages.Add(img);
                }

                // delete any images from the database which are no longer valid
                foreach (TvDB_ImagePoster img in RepoFactory.TvDB_ImagePoster.GetBySeriesID(seriesID))
                {
                    if (!validIDs.Contains(img.Id))
                        RepoFactory.TvDB_ImageFanart.Delete(img.TvDB_ImagePosterID);
                }
            }
            catch (TvDbSharper.TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                    {
                        return await GetPosterOnlineAsync(seriesID);
                    }
                    // suppress 404 and move on
                }
                else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return tvImages;
                logger.Error(exception, "TvDB returned an error code: " + exception.StatusCode + "\n        " + exception.Message);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TVDBApiHelper.GetPosterOnlineAsync: " + ex.ToString());
            }

            return tvImages;
        }

        public static List<TvDB_ImageWideBanner> GetBannerOnline(int seriesID)
        {
            return Task.Run(async () => await GetBannerOnlineAsync(seriesID)).Result;
        }

        public static async Task<List<TvDB_ImageWideBanner>> GetBannerOnlineAsync(int seriesID)
        {
            List<int> validIDs = new List<int>();
            List<TvDB_ImageWideBanner> tvImages = new List<TvDB_ImageWideBanner>();

            try
            {
                Image[] season = await GetSeriesImagesAsync(seriesID, KeyType.Seasonwide);
                Image[] series = await GetSeriesImagesAsync(seriesID, KeyType.Series);

                Image[] images = season.Concat(series).ToArray();

                foreach (Image image in images)
                {
                    int id = image.Id ?? 0;
                    if (id == 0) { continue; }

                    TvDB_ImageWideBanner img = RepoFactory.TvDB_ImageWideBanner.GetByTvDBID(id);

                    if (img == null)
                    {
                        img = new TvDB_ImageWideBanner
                        {
                            Enabled = 1
                        };
                    }

                    img.Populate(seriesID, image);
                    img.Language = client.AcceptedLanguage;
                    RepoFactory.TvDB_ImageWideBanner.Save(img);
                    validIDs.Add(id);
                    tvImages.Add(img);
                }

                // delete any images from the database which are no longer valid
                foreach (TvDB_ImageWideBanner img in RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(seriesID))
                {
                    if (!validIDs.Contains(img.Id))
                        RepoFactory.TvDB_ImageFanart.Delete(img.TvDB_ImageWideBannerID);
                }
            }
            catch (TvDbSharper.TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetBannerOnlineAsync(seriesID);
                    // suppress 404 and move on
                }
                else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return tvImages;
                logger.Error(exception, "TvDB returned an error code: " + exception.StatusCode + "\n        " + exception.Message);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TVDBApiHelper.GetPosterOnlineAsync: " + ex.ToString());
            }

            return tvImages;
        }

        public static void DownloadAutomaticImages(List<TvDB_ImageFanart> images, int seriesID, bool forceDownload)
        {
            int imageCount = 0;

            // find out how many images we already have locally
            foreach (TvDB_ImageFanart fanart in RepoFactory.TvDB_ImageFanart.GetBySeriesID(seriesID))
            {
                if (!string.IsNullOrEmpty(fanart.GetFullImagePath()) && File.Exists(fanart.GetFullImagePath()))
                    imageCount++;
            }

            foreach (TvDB_ImageFanart img in images)
            {
                if (ServerSettings.TvDB_AutoFanart && imageCount < ServerSettings.TvDB_AutoFanartAmount)
                {
                    bool fileExists = File.Exists(img.GetFullImagePath());
                    if (!fileExists || (fileExists && forceDownload))
                    {
                        CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(img.TvDB_ImageFanartID,
                            ImageEntityType.TvDB_FanArt, forceDownload);
                        cmd.Save();
                        imageCount++;
                    }
                }
                else
                {
                    //The TvDB_AutoFanartAmount point to download less images than its available
                    // we should clean those image that we didn't download because those dont exists in local repo
                    // first we check if file was downloaded
                    if (!File.Exists(img.GetFullImagePath()))
                    {
                        RepoFactory.TvDB_ImageFanart.Delete(img.TvDB_ImageFanartID);
                    }
                }
            }
        }

        public static void DownloadAutomaticImages(List<TvDB_ImagePoster> images, int seriesID, bool forceDownload)
        {
            int imageCount = 0;

            // find out how many images we already have locally
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                ISessionWrapper sessionWrapper = session.Wrap();

                foreach (TvDB_ImagePoster fanart in RepoFactory.TvDB_ImagePoster
                    .GetBySeriesID(seriesID))
                {
                    if (!string.IsNullOrEmpty(fanart.GetFullImagePath()) && File.Exists(fanart.GetFullImagePath()))
                        imageCount++;
                }
            }

            foreach (TvDB_ImagePoster img in images)
            {
                if (ServerSettings.TvDB_AutoFanart && imageCount < ServerSettings.TvDB_AutoFanartAmount)
                {
                    bool fileExists = File.Exists(img.GetFullImagePath());
                    if (!fileExists || (fileExists && forceDownload))
                    {
                        CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(img.TvDB_ImagePosterID,
                            ImageEntityType.TvDB_Cover, forceDownload);
                        cmd.Save();
                        imageCount++;
                    }
                }
                else
                {
                    //The TvDB_AutoFanartAmount point to download less images than its available
                    // we should clean those image that we didn't download because those dont exists in local repo
                    // first we check if file was downloaded
                    if (!File.Exists(img.GetFullImagePath()))
                    {
                        RepoFactory.TvDB_ImageFanart.Delete(img.TvDB_ImagePosterID);
                    }
                }
            }
        }

        public static void DownloadAutomaticImages(List<TvDB_ImageWideBanner> images, int seriesID, bool forceDownload)
        {
            int imageCount = 0;

            // find out how many images we already have locally
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                ISessionWrapper sessionWrapper = session.Wrap();

                foreach (TvDB_ImageWideBanner banner in RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(seriesID))
                {
                    if (!string.IsNullOrEmpty(banner.GetFullImagePath()) && File.Exists(banner.GetFullImagePath()))
                        imageCount++;
                }
            }

            foreach (TvDB_ImageWideBanner img in images)
            {
                if (ServerSettings.TvDB_AutoFanart && imageCount < ServerSettings.TvDB_AutoFanartAmount)
                {
                    bool fileExists = File.Exists(img.GetFullImagePath());
                    if (!fileExists || (fileExists && forceDownload))
                    {
                        CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(img.TvDB_ImageWideBannerID,
                            ImageEntityType.TvDB_Banner, forceDownload);
                        cmd.Save();
                        imageCount++;
                    }
                }
                else
                {
                    //The TvDB_AutoFanartAmount point to download less images than its available
                    // we should clean those image that we didn't download because those dont exists in local repo
                    // first we check if file was downloaded
                    if (!File.Exists(img.GetFullImagePath()))
                    {
                        RepoFactory.TvDB_ImageFanart.Delete(img.TvDB_ImageWideBannerID);
                    }
                }
            }
        }

        public static List<BasicEpisode> GetEpisodesOnline(int seriesID)
        {
            return Task.Run(async () => await GetEpisodesOnlineAsync(seriesID)).Result;
        }

        static async Task<List<BasicEpisode>> GetEpisodesOnlineAsync(int seriesID)
        {
            List<BasicEpisode> apiEpisodes = new List<BasicEpisode>();
            try
            {
                await CheckAuthorizationAsync();

                var tasks = new List<Task<TvDbResponse<BasicEpisode[]>>>();
                TvDBRateLimiter.Instance.EnsureRate();
                var firstResponse = await client.Series.GetEpisodesAsync(seriesID, 1);

                for (int i = 2; i <= firstResponse.Links.Last; i++)
                {
                    TvDBRateLimiter.Instance.EnsureRate();
                    tasks.Add(client.Series.GetEpisodesAsync(seriesID, i));
                }

                var results = await Task.WhenAll(tasks);

                apiEpisodes = firstResponse.Data.Concat(results.SelectMany(x => x.Data)).ToList();
            }
            catch (TvDbSharper.TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetEpisodesOnlineAsync(seriesID);
                    // suppress 404 and move on
                }
                else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return apiEpisodes;
                logger.Error(exception, "TvDB returned an error code: " + exception.StatusCode + "\n        " + exception.Message);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TvDBApiHelper.GetEpisodesOnlineAsync: " + ex.ToString());
            }

            return apiEpisodes;
        }

        static EpisodeRecord GetEpisodeDetails(int episodeID)
        {
            return Task.Run(async () => await GetEpisodeDetailsAsync(episodeID)).Result;
        }

        static async Task<EpisodeRecord> GetEpisodeDetailsAsync(int episodeID)
        {
            try
            {
                await CheckAuthorizationAsync();

                TvDBRateLimiter.Instance.EnsureRate();
                var response = await client.Episodes.GetAsync(episodeID);
                return response.Data;
            }
            catch (TvDbSharper.TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                        return await GetEpisodeDetailsAsync(episodeID);
                    // suppress 404 and move on
                }
                else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return null;
                logger.Error(exception, "TvDB returned an error code: " + exception.StatusCode + "\n        " + exception.Message);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TvDBApiHelper.GetEpisodeDetailsAsync: " + ex.ToString());
            }

            return null;
        }

        public static async Task QueueEpisodeImageDownloadAsync(BasicEpisode item, List<int> existingEpIds, bool downloadImages, bool forceRefresh)
        {
            try
            {
                // the episode id
                int id = item.Id;
                existingEpIds.Add(id);

                TvDB_Episode ep = RepoFactory.TvDB_Episode.GetByTvDBID(id);
                if (ep == null)
                    ep = new TvDB_Episode();

                EpisodeRecord episode = await GetEpisodeDetailsAsync(id);
                if (episode == null)
                    return;
                ep.Populate(episode);
                RepoFactory.TvDB_Episode.Save(ep);

                if (downloadImages)
                {
                    // download the image for this episode
                    if (!string.IsNullOrEmpty(ep.Filename))
                    {
                        bool fileExists = File.Exists(ep.GetFullImagePath());
                        if (!fileExists || forceRefresh)
                        {
                            CommandRequest_DownloadImage cmd =
                                new CommandRequest_DownloadImage(ep.TvDB_EpisodeID,
                                    ImageEntityType.TvDB_Episode, forceRefresh);
                            cmd.Save();
                        }
                    }
                }
            }
            catch (TvDbSharper.TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                    {
                        await QueueEpisodeImageDownloadAsync(item, existingEpIds, downloadImages, forceRefresh);
                        return;
                    }
                    // suppress 404 and move on
                }
                else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return;
                logger.Error(exception, "TvDB returned an error code: " + exception.StatusCode + "\n        " + exception.Message);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TVDBHelper.GetEpisodes: " + ex.ToString());
            }
        }

        public static void UpdateAllInfoAndImages(int seriesID, bool forceRefresh, bool downloadImages)
        {
            Task.Run(() => UpdateAllInfoAndImagesAsync(seriesID, forceRefresh, downloadImages)).Wait();
        }

        public static async Task UpdateAllInfoAndImagesAsync(int seriesID, bool forceRefresh, bool downloadImages)
        {
            try
            {
                // update the series info
                TvDB_Series tvSeries = GetSeriesInfoOnline(seriesID);
                if (tvSeries == null) return;

                if (downloadImages)
                {
                    DownloadAutomaticImages(seriesID, forceRefresh);
                }

                // update all the episodes and download episode images
                // TODO: only basic episode info is provided here 
                List<BasicEpisode> episodeItems = GetEpisodesOnline(seriesID);
                logger.Trace("Found {0} Episode nodes", episodeItems.Count.ToString());

                List<int> existingEpIds = new List<int>();
                foreach (BasicEpisode item in episodeItems)
                {
                    await QueueEpisodeImageDownloadAsync(item, existingEpIds, downloadImages, forceRefresh);
                }

                // get all the existing tvdb episodes, to see if any have been deleted
                List<TvDB_Episode> allEps = RepoFactory.TvDB_Episode.GetBySeriesID(seriesID);
                foreach (TvDB_Episode oldEp in allEps)
                {
                    if (!existingEpIds.Contains(oldEp.Id))
                        RepoFactory.TvDB_Episode.Delete(oldEp.TvDB_EpisodeID);
                }

                var xref = RepoFactory.CrossRef_AniDB_TvDBV2.GetByTvDBID(tvSeries.SeriesID).FirstOrDefault();
                if (xref == null) return;
                var anime = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID);
                var episodes = RepoFactory.AnimeEpisode.GetBySeriesID(anime.AnimeSeriesID);
                foreach (SVR_AnimeEpisode episode in episodes)
                {
                    RepoFactory.AnimeEpisode.Save(episode);
                }
                SVR_AniDB_Anime.UpdateStatsByAnimeID(xref.AnimeID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TVDBHelper.GetEpisodes: " + ex.ToString());
            }
        }

        public static void LinkAniDBTvDBEpisode(int aniDBID, int tvDBID, int animeID)
        {
            CrossRef_AniDB_TvDB_Episode xref = RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAniDBEpisodeID(aniDBID);
            if (xref == null)
                xref = new CrossRef_AniDB_TvDB_Episode();

            xref.AnimeID = animeID;
            xref.AniDBEpisodeID = aniDBID;
            xref.TvDBEpisodeID = tvDBID;
            RepoFactory.CrossRef_AniDB_TvDB_Episode.Save(xref);

            SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);

            SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(aniDBID);
            RepoFactory.AnimeEpisode.Save(ep);

            logger.Trace("Changed tvdb episode association: {0}", aniDBID);
        }

        // Removes all TVDB information from a series, bringing it back to a blank state.
        public static void RemoveLinkAniDBTvDB(int animeID, EpisodeType aniEpType, int aniEpNumber, int tvDBID,
            int tvSeasonNumber, int tvEpNumber)
        {
            CrossRef_AniDB_TvDBV2 xref = RepoFactory.CrossRef_AniDB_TvDBV2.GetByTvDBID(tvDBID, tvSeasonNumber,
                tvEpNumber, animeID,
                (int)aniEpType,
                aniEpNumber);
            if (xref == null) return;

            RepoFactory.CrossRef_AniDB_TvDBV2.Delete(xref.CrossRef_AniDB_TvDBV2ID);

            SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);

            CommandRequest_WebCacheDeleteXRefAniDBTvDB req = new CommandRequest_WebCacheDeleteXRefAniDBTvDB(animeID,
                (int)aniEpType, aniEpNumber,
                tvDBID, tvSeasonNumber, tvEpNumber);
            req.Save();
        }

        public static void ScanForMatches()
        {
            IReadOnlyList<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll();

            IReadOnlyList<CrossRef_AniDB_TvDBV2> allCrossRefs = RepoFactory.CrossRef_AniDB_TvDBV2.GetAll();
            List<int> alreadyLinked = new List<int>();
            foreach (CrossRef_AniDB_TvDBV2 xref in allCrossRefs)
            {
                alreadyLinked.Add(xref.AnimeID);
            }

            foreach (SVR_AnimeSeries ser in allSeries)
            {
                if (alreadyLinked.Contains(ser.AniDB_ID)) continue;

                SVR_AniDB_Anime anime = ser.GetAnime();

                if (anime != null)
                {
                    if (!anime.GetSearchOnTvDB()) continue; // Don't log if it isn't supposed to be there
                    logger.Trace("Found anime without tvDB association: " + anime.MainTitle);
                    if (anime.IsTvDBLinkDisabled())
                    {
                        logger.Trace("Skipping scan tvDB link because it is disabled: " + anime.MainTitle);
                        continue;
                    }
                }

                CommandRequest_TvDBSearchAnime cmd = new CommandRequest_TvDBSearchAnime(ser.AniDB_ID, false);
                cmd.Save();
            }
        }

        public static void UpdateAllInfo(bool force)
        {
            IReadOnlyList<CrossRef_AniDB_TvDBV2> allCrossRefs = RepoFactory.CrossRef_AniDB_TvDBV2.GetAll();
            List<int> alreadyLinked = new List<int>();
            foreach (CrossRef_AniDB_TvDBV2 xref in allCrossRefs)
            {
                CommandRequest_TvDBUpdateSeriesAndEpisodes cmd =
                    new CommandRequest_TvDBUpdateSeriesAndEpisodes(xref.TvDBID, force);
                cmd.Save();
            }
        }

        public static List<int> GetUpdatedSeriesList(string serverTime)
        {
            return Task.Run(async () => await GetUpdatedSeriesListAsync(serverTime)).Result;
        }

        public static async Task<List<int>> GetUpdatedSeriesListAsync(string serverTime)
        {
            List<int> seriesList = new List<int>();
            try
            {
                // Unix timestamp is seconds past epoch
                DateTime lastUpdateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                lastUpdateTime = lastUpdateTime.AddSeconds(long.Parse(serverTime)).ToLocalTime();
                TvDBRateLimiter.Instance.EnsureRate();
                var response = await client.Updates.GetAsync(lastUpdateTime);

                Update[] updates = response?.Data;
                if (updates == null) return seriesList;

                foreach (Update item in updates)
                {
                    if (item == null) continue;
                    seriesList.Add(item.Id);
                }

                return seriesList;
            }
            catch (TvDbSharper.TvDbServerException exception)
            {
                if (exception.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    client.Authentication.Token = null;
                    await CheckAuthorizationAsync();
                    if (!string.IsNullOrEmpty(client.Authentication.Token))
                    {
                        return await GetUpdatedSeriesListAsync(serverTime);
                    }
                    // suppress 404 and move on
                }
                else if (exception.StatusCode == (int)HttpStatusCode.NotFound) return seriesList;
                logger.Error(exception, "TvDB returned an error code: " + exception.StatusCode + "\n        " + exception.Message);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in GetUpdatedSeriesList: " + ex.ToString());
            }
            return seriesList;
        }

        public static string IncrementalTvDBUpdate(ref List<int> tvDBIDs, ref bool tvDBOnline)
        {
            // check if we have record of doing an automated update for the TvDB previously
            // if we have then we have kept a record of the server time and can do a delta update
            // otherwise we need to do a full update and keep a record of the time

            List<int> allTvDBIDs = new List<int>();
            tvDBIDs = new List<int>();
            tvDBOnline = true;

            try
            {
                // record the tvdb server time when we started
                // we record the time now instead of after we finish, to include any possible misses
                string currentTvDBServerTime = CurrentServerTime;
                if (currentTvDBServerTime.Length == 0)
                {
                    tvDBOnline = false;
                    return currentTvDBServerTime;
                }

                foreach (SVR_AnimeSeries ser in RepoFactory.AnimeSeries.GetAll())
                {
                    List<CrossRef_AniDB_TvDBV2> xrefs = ser.GetCrossRefTvDBV2();
                    if (xrefs == null) continue;

                    foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
                    {
                        if (!allTvDBIDs.Contains(xref.TvDBID)) allTvDBIDs.Add(xref.TvDBID);
                    }
                }

                // get the time we last did a TvDB update
                // if this is the first time it will be null
                // update the anidb info ever 24 hours

                ScheduledUpdate sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TvDBInfo);

                string lastServerTime = "";
                if (sched != null)
                {
                    TimeSpan ts = DateTime.Now - sched.LastUpdate;
                    logger.Trace("Last tvdb info update was {0} hours ago", ts.TotalHours.ToString());
                    if (!string.IsNullOrEmpty(sched.UpdateDetails))
                        lastServerTime = sched.UpdateDetails;

                    // the UpdateDetails field for this type will actually contain the last server time from
                    // TheTvDB that a full update was performed
                }


                // get a list of updates from TvDB since that time
                if (lastServerTime.Length > 0)
                {
                    List<int> seriesList = GetUpdatedSeriesList(lastServerTime);
                    logger.Trace("{0} series have been updated since last download", seriesList.Count.ToString());
                    logger.Trace("{0} TvDB series locally", allTvDBIDs.Count.ToString());

                    foreach (int id in seriesList)
                    {
                        if (allTvDBIDs.Contains(id)) tvDBIDs.Add(id);
                    }
                    logger.Trace("{0} TvDB local series have been updated since last download",
                        tvDBIDs.Count.ToString());
                }
                else
                {
                    // use the full list
                    tvDBIDs = allTvDBIDs;
                }

                return currentTvDBServerTime;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "IncrementalTvDBUpdate: " + ex.ToString());
                return "";
            }
        }
    }
}