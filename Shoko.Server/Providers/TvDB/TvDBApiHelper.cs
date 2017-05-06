using Shoko.Models.Server;
using TvDbSharper;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Server.Repositories;
using Shoko.Server.Extensions;
using Shoko.Models.TvDB;
using Shoko.Models.Enums;
using Shoko.Server.Models;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Providers.TvDB
{
    public class TvDBApiHelper
    {
        static ITvDbClient client = new TvDbClient();
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public TvDBApiHelper()
        {
        }

        private static async Task _checkAuthorizationAsync()
        {
            client.AcceptedLanguage = ServerSettings.TvDB_Language;
            if (client.Authentication.Token == null)
            {
                await client.Authentication.AuthenticateAsync(Constants.TvDBURLs.apiKey);
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
                await _checkAuthorizationAsync();

                logger.Trace("GetSeriesInfo: {0}", seriesID);
                var response = await client.Series.GetAsync(seriesID);
                TvDbSharper.Clients.Series.Json.Series series = response.Data;

                TvDB_Series tvSeries = RepoFactory.TvDB_Series.GetByTvDBID(seriesID);
                if (tvSeries == null)
                    tvSeries = new TvDB_Series();

                tvSeries.PopulateFromSeriesInfo(series);
                RepoFactory.TvDB_Series.Save(tvSeries);
                
                return tvSeries;
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
                await _checkAuthorizationAsync();

                // Search for a series
                string url = string.Format(Shoko.Server.Constants.TvDBURLs.urlSeriesSearch, criteria);
                logger.Trace("Search TvDB Series: {0}", criteria);

                var response = await client.Search.SearchSeriesByNameAsync(criteria);
                TvDbSharper.Clients.Search.Json.SeriesSearchResult[] series = response.Data;

                foreach (TvDbSharper.Clients.Search.Json.SeriesSearchResult item in series) {
                    TVDB_Series_Search_Response searchResult = new TVDB_Series_Search_Response();
                    searchResult.Populate(item);
                    results.Add(searchResult);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in SearchSeries: " + ex.ToString());
            }

            return results;
        }

        public static string LinkAniDBTvDB(int animeID, enEpisodeType aniEpType, int aniEpNumber, int tvDBID,
            int tvSeasonNumber, int tvEpNumber, bool excludeFromWebCache, bool additiveLink = false)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                if (!additiveLink)
                    // remove all current links
                    RemoveAllAniDBTvDBLinks(session.Wrap(), animeID);

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

                CrossRef_AniDB_TvDBV2 xref = RepoFactory.CrossRef_AniDB_TvDBV2.GetByTvDBID(session, tvDBID,
                    tvSeasonNumber, tvEpNumber,
                    animeID,
                    (int)aniEpType, aniEpNumber);
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

                SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);

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

        public static void RemoveAllAniDBTvDBLinks(ISessionWrapper session, int animeID, int aniEpType = -1)
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

            List<CrossRef_AniDB_TvDBV2> xrefs = RepoFactory.CrossRef_AniDB_TvDBV2.GetByAnimeID(session, animeID);
            if (xrefs == null || xrefs.Count == 0) return;

            foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
            {
                if (aniEpType != -1 && aniEpType == xref.AniDBStartEpisodeType) continue;

                RepoFactory.CrossRef_AniDB_TvDBV2.Delete(xref.CrossRef_AniDB_TvDBV2ID);

                if (aniEpType == -1)
                {
                    foreach (enEpisodeType eptype in Enum.GetValues(typeof(enEpisodeType)))
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

            SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);
        }
    }
}
