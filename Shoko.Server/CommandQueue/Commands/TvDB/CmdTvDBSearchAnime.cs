using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Models.TvDB;
using Shoko.Models.WebCache;
using Shoko.Server.Models;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Providers.WebCache;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.CommandQueue.Commands.TvDB
{
    public class CmdTvDBSearchAnime : BaseCommand, ICommand
    {
        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }
        public string ParallelTag { get; set; } = WorkTypes.TvDB;
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 6;

        public string Id => $"TvDBSearchAnime_{AnimeID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.SearchTvDB,
            ExtraParams = new[] {AnimeID.ToString(), ForceRefresh.ToString()}
        };

        public string WorkType => WorkTypes.TvDB;

        public CmdTvDBSearchAnime(string str) : base(str)
        {
        }

        public CmdTvDBSearchAnime(int animeID, bool forced)
        {
            AnimeID = animeID;
            ForceRefresh = forced;
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_TvDBSearchAnime: {0}", AnimeID);

            try
            {
                ReportInit(progress);
                // first check if the user wants to use the web cache
                if (ServerSettings.Instance.WebCache.TvDB_Get)
                {
                    try
                    {
                        //NO OP
                        //Till Webache operational
                        /*
                        List<WebCache_CrossRef_AniDB_TvDB> cacheResults =
                            WebCacheAPI.Get_CrossRefAniDBTvDB(AnimeID);
                        ReportUpdate(progress,25);
                        if (cacheResults != null && cacheResults.Count > 0)
                        {
                            // check again to see if there are any links, user may have manually added links while
                            // this command was in the queue
                            List<CrossRef_AniDB_TvDB> xrefTemp =
                                Repo.Instance.CrossRef_AniDB_TvDB.GetByAnimeID(AnimeID);
                            if (xrefTemp != null && xrefTemp.Count > 0)
                            {
                                ReportFinish(progress);
                                return;
                            }

                            // Add overrides for specials
                            var specialXRefs = cacheResults.Where(a => a.TvDBSeasonNumber == 0)
                                .OrderBy(a => a.AniDBStartEpisodeType).ThenBy(a => a.AniDBStartEpisodeNumber)
                                .ToList();
                            ReportUpdate(progress, 50);
                            if (specialXRefs.Count != 0)
                            {
                                foreach (var episodeOverride in TvDBLinkingHelper.GetSpecialsOverridesFromLegacy(specialXRefs))
                                {
                                    var exists =
                                        Repo.Instance.CrossRef_AniDB_TvDB_Episode_Override.GetByAniDBAndTvDBEpisodeIDs(
                                            episodeOverride.AniDBEpisodeID, episodeOverride.TvDBEpisodeID);
                                    if (exists != null) continue;
                                    Repo.Instance.CrossRef_AniDB_TvDB_Episode_Override.Touch(() => episodeOverride);
                                }
                            }
                            ReportUpdate(progress, 75);
                            foreach (WebCache_CrossRef_AniDB_TvDB xref in cacheResults)
                            {
                                TvDB_Series tvser = TvDBApiHelper.GetSeriesInfoOnline(xref.TvDBID, false);
                                if (tvser != null)
                                {
                                    logger.Trace("Found tvdb match on web cache for {0}", AnimeID);
                                    TvDBApiHelper.LinkAniDBTvDB(AnimeID, xref.TvDBID, true);
                                }
                            }
                            ReportFinish(progress);
                            return;
                        }
                            */
                    }
                    catch (Exception)
                    {
                        //Ignore
                    }
                }

                if (!ServerSettings.Instance.TvDB.AutoLink)
                {
                    ReportFinish(progress);
                    return;
                }

                // try to pull a link from a prequel/sequel
                var relations = Repo.Instance.AniDB_Anime_Relation.GetFullLinearRelationTree(AnimeID);
                string tvDBID = relations.SelectMany(a => Repo.Instance.CrossRef_AniDB_Provider.GetByAnimeIDAndType(a,CrossRefType.TvDB))
                    .FirstOrDefault(a => a != null)?.CrossRefID;
                ReportUpdate(progress, 25);

                if (tvDBID != null)
                {
                    TvDBApiHelper.LinkAniDBTvDB(AnimeID, int.Parse(tvDBID), true);
                    ReportFinish(progress);
                    return;
                }
                ReportUpdate(progress, 50);

                // search TvDB
                SVR_AniDB_Anime anime = Repo.Instance.AniDB_Anime.GetByAnimeID(AnimeID);
                if (anime == null)
                {
                    ReportFinish(progress);
                    return;
                }

                var searchCriteria = anime.MainTitle;

                // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
                List<TVDB_Series_Search_Response> results = TvDBApiHelper.SearchSeries(searchCriteria);
                logger.Trace("Found {0} tvdb results for {1} on TheTvDB", results.Count, searchCriteria);
                if (ProcessSearchResults(results, searchCriteria))
                {
                    ReportFinish(progress);
                    return;
                }


                if (results.Count != 0)
                {
                    ReportFinish(progress);
                    return;
                }

                bool foundResult = false;
                ReportUpdate(progress, 75);
                foreach (AniDB_Anime_Title title in anime.GetTitles())
                {
                    if (!title.TitleType.Equals(Shoko.Models.Constants.AnimeTitleType.Official, StringComparison.InvariantCultureIgnoreCase))
                        continue;
                    if (!title.Language.Equals(Shoko.Models.Constants.AniDBLanguageType.English,
                            StringComparison.InvariantCultureIgnoreCase) &&
                        !title.Language.Equals(Shoko.Models.Constants.AniDBLanguageType.Romaji,
                            StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    if (searchCriteria.Equals(title.Title, StringComparison.InvariantCultureIgnoreCase)) continue;

                    searchCriteria = title.Title;
                    results = TvDBApiHelper.SearchSeries(searchCriteria);
                    if (results.Count > 0) foundResult = true;
                    logger.Trace("Found {0} tvdb results for search on {1}", results.Count, title.Title);
                    if (ProcessSearchResults(results, title.Title))
                    {
                        ReportFinish(progress);
                        return;
                    } 
                }
                if (!foundResult) logger.Warn("Unable to find a matching TvDB series for {0}", anime.MainTitle);
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CommandRequest_TvDBSearchAnime: {AnimeID} - {ex}", ex);
            }
        }

        private bool ProcessSearchResults(List<TVDB_Series_Search_Response> results, string searchCriteria)
        {
            switch (results.Count)
            {
                case 1:
                    // since we are using this result, lets download the info
                    logger.Trace("Found 1 tvdb results for search on {0} --- Linked to {1} ({2})", searchCriteria,
                        results[0].SeriesName,
                        results[0].SeriesID);
                     TvDBApiHelper.GetSeriesInfoOnline(results[0].SeriesID, false);
                    TvDBApiHelper.LinkAniDBTvDB(AnimeID, results[0].SeriesID, true);

                    // add links for multiple seasons (for long shows)
                    AddCrossRef_AniDB_TvDBV2(AnimeID, results[0].SeriesID, CrossRefSource.Automatic);
                    SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);
                    return true;
                case 0:
                    return false;
                default:
                    logger.Trace("Found multiple ({0}) tvdb results for search on so checking for english results {1}",
                        results.Count,
                        searchCriteria);
                    foreach (TVDB_Series_Search_Response sres in results)
                    {
                        // since we are using this result, lets download the info
                        logger.Trace("Found english result for search on {0} --- Linked to {1} ({2})", searchCriteria,
                            sres.SeriesName,
                            sres.SeriesID);
                        TvDBApiHelper.GetSeriesInfoOnline(results[0].SeriesID, false);
                        TvDBApiHelper.LinkAniDBTvDB(AnimeID, sres.SeriesID, true);

                        // add links for multiple seasons (for long shows)
                        AddCrossRef_AniDB_TvDBV2(AnimeID, results[0].SeriesID, CrossRefSource.Automatic);
                        SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);
                        return true;
                    }

                    logger.Trace("No english results found, so SKIPPING: {0}", searchCriteria);

                    return false;
            }
        }

        private static void AddCrossRef_AniDB_TvDBV2(int animeID, int tvdbID, CrossRefSource source)
        {
            if (Repo.Instance.CrossRef_AniDB_Provider.GetByAnimeIdAndProvider(CrossRefType.TvDB, animeID, tvdbID.ToString())==null) return;

            SVR_CrossRef_AniDB_Provider xref = new SVR_CrossRef_AniDB_Provider
            {
                AnimeID = animeID,
                CrossRefID = tvdbID.ToString(),
                CrossRefSource = source,
                CrossRefType = CrossRefType.TvDB
            };
            Repo.Instance.CrossRef_AniDB_Provider.BeginAdd(xref).Commit();
        }

    }
}
