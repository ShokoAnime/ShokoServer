using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Models.WebCache;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TraktTV.Contracts;
using Shoko.Server.Providers.TraktTV.Contracts.Search;
using Shoko.Server.Providers.WebCache;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.CommandQueue.Commands.Trakt
{

    public class CmdTraktSearchAnime : BaseCommand, ICommand
    {
        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.Trakt;
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 6;

        public string Id => $"TraktSearchAnime_{AnimeID}";

        public  QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.SearchTrakt,
            ExtraParams = new[] {AnimeID.ToString(), ForceRefresh.ToString()}
        };

        public string WorkType => WorkTypes.Trakt;

        public CmdTraktSearchAnime(string str) : base(str)
        {
        }

        public CmdTraktSearchAnime(int animeID, bool forced)
        {
            AnimeID = animeID;
            ForceRefresh = forced;
        }


        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_TraktSearchAnime: {0}", AnimeID);

            try
            {
                ReportInit(progress);
                bool doReturn = false;

                // first check if the user wants to use the web cache
                if (ServerSettings.Instance.WebCache.Trakt_Get)
                {
                    try
                    {

                        List<WebCache_CrossRef_AniDB_Provider> resultsCache = WebCacheAPI.Instance.GetCrossRef_AniDB_Provider(AnimeID, CrossRefType.TraktTV);
                        ReportUpdate(progress, 30);
                        if (resultsCache != null && resultsCache.Count > 0)
                        {
                            List<WebCache_CrossRef_AniDB_Provider> best = resultsCache.BestProvider();
                            if (best.Count > 0)
                            {
                                TraktTVHelper.RemoveAllAniDBTraktLinks(AnimeID, false);
                                ReportUpdate(progress, 70);
                                foreach (WebCache_CrossRef_AniDB_Provider xref in best)
                                    TraktTVHelper.LinkAniDBTraktFromWebCache(xref);
                                SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);
                                logger.Trace("Changed trakt association: {0}", AnimeID);
                                ReportFinish(progress);
                                return;
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, ex.ToString());
                    }
                }


                // lets try to see locally if we have a tvDB link for this anime
                // Trakt allows the use of TvDB ID's or their own Trakt ID's
                List<SVR_CrossRef_AniDB_Provider>
                    xrefTvDBs = Repo.Instance.CrossRef_AniDB_Provider.GetByAnimeIDAndType(AnimeID, CrossRefType.TvDB);
                if (xrefTvDBs != null && xrefTvDBs.Count > 0)
                {
                    foreach (SVR_CrossRef_AniDB_Provider tvXRef in xrefTvDBs)
                    {
                        // first search for this show by the TvDB ID
                        List<TraktV2SearchTvDBIDShowResult> searchResults = TraktTVHelper.SearchShowByIDV2(TraktSearchIDType.tvdb,tvXRef.CrossRefID);
                        if (searchResults == null || searchResults.Count <= 0) continue;
                        // since we are searching by ID, there will only be one 'show' result
                        TraktV2Show resShow = null;
                        foreach (TraktV2SearchTvDBIDShowResult res in searchResults)
                        {
                            if (res.ResultType != SearchIDType.Show) continue;
                            resShow = res.show;
                            break;
                        }

                        if (resShow == null) continue;

                        TraktV2ShowExtended showInfo = TraktTVHelper.GetShowInfoV2(resShow.ids.slug);
                        if (showInfo?.ids == null) continue;

                        // make sure the season specified by TvDB also exists on Trakt
                        Trakt_Show traktShow =
                            Repo.Instance.Trakt_Show.GetByTraktSlug(showInfo.ids.slug);
                        if (traktShow == null) continue;

                        Trakt_Season traktSeason = Repo.Instance.Trakt_Season.GetByShowIDAndSeason(traktShow.Trakt_ShowID,tvXRef.GetEpisodesWithOverrides().FirstOrDefault()?.Season ?? 0);
                        if (traktSeason == null) continue;

                        logger.Trace("Found trakt match using TvDBID locally {0} - id = {1}",
                            AnimeID, showInfo.title);
                        TraktTVHelper.LinkAniDBTrakt(AnimeID,showInfo.ids.slug,true);
                        doReturn = true;
                        ReportUpdate(progress,60);
                    }
                    if (doReturn) 
                    {
                        ReportFinish(progress);
                        return;
                    }
                }

                // Use TvDB setting due to similarity
                if (!ServerSettings.Instance.TvDB.AutoLink)
                {
                    ReportFinish(progress);
                    return;
                }

                // finally lets try searching Trakt directly
                SVR_AniDB_Anime anime = Repo.Instance.AniDB_Anime.GetByAnimeID(AnimeID);
                if (anime == null)
                {
                    ReportFinish(progress);
                    return;
                }

                var searchCriteria = anime.MainTitle;

                // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
                List<TraktV2SearchShowResult> results = TraktTVHelper.SearchShowV2(searchCriteria);
                logger.Trace("Found {0} trakt results for {1} ", results.Count, searchCriteria);
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

                ReportUpdate(progress, 80);
                foreach (AniDB_Anime_Title title in anime.GetTitles())
                {
                    if (!string.Equals(title.TitleType, Shoko.Models.Constants.AnimeTitleType.Official, StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    if (string.Equals(searchCriteria, title.Title, StringComparison.InvariantCultureIgnoreCase)) continue;

                    results = TraktTVHelper.SearchShowV2(searchCriteria);
                    logger.Trace("Found {0} trakt results for search on {1}", results.Count, title.Title);
                    if (ProcessSearchResults(results, title.Title))
                    {
                        ReportFinish(progress);
                        return;
                    }
                }

                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CommandRequest_TvDBSearchAnime: {AnimeID} - {ForceRefresh} - {ex}", ex);
            }
        }

        private bool ProcessSearchResults(List<TraktV2SearchShowResult> results,
            string searchCriteria)
        {
            if (results.Count == 1)
            {
                if (results[0].show != null)
                {
                    // since we are using this result, lets download the info
                    logger.Trace("Found 1 trakt results for search on {0} --- Linked to {1} ({2})", searchCriteria,
                        results[0].show.Title, results[0].show.ids.slug);
                    TraktV2ShowExtended showInfo = TraktTVHelper.GetShowInfoV2(results[0].show.ids.slug);
                    if (showInfo != null)
                    {
                        TraktTVHelper.LinkAniDBTrakt(AnimeID, results[0].show.ids.slug, true);
                        return true;
                    }
                }
            }

            return false;
        }

    }
}
