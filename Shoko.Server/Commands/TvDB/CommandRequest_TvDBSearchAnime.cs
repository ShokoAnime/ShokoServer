using System;
using System.Collections.Generic;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Azure;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Models.TvDB;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_TvDBSearchAnime : CommandRequest_TvDBBase
    {
        public virtual int AnimeID { get; set; }
        public virtual bool ForceRefresh { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.SearchTvDB,
            extraParams = new[] {AnimeID.ToString()}
        };

        public CommandRequest_TvDBSearchAnime()
        {
        }

        public CommandRequest_TvDBSearchAnime(int animeID, bool forced)
        {
            AnimeID = animeID;
            ForceRefresh = forced;
            CommandType = (int) CommandRequestType.TvDB_SearchAnime;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TvDBSearchAnime: {0}", AnimeID);

            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    // first check if the user wants to use the web cache
                    if (ServerSettings.WebCache_TvDB_Get)
                    {
                        try
                        {
                            List<Azure_CrossRef_AniDB_TvDB> cacheResults =
                                AzureWebAPI.Get_CrossRefAniDBTvDB(AnimeID);
                            if (cacheResults != null && cacheResults.Count > 0)
                            {
                                // check again to see if there are any links, user may have manually added links while
                                // this command was in the queue
                                List<CrossRef_AniDB_TvDBV2> xrefTemp =
                                    RepoFactory.CrossRef_AniDB_TvDBV2.GetByAnimeID(AnimeID);
                                if (xrefTemp != null && xrefTemp.Count > 0) return;

                                foreach (Azure_CrossRef_AniDB_TvDB xref in cacheResults)
                                {
                                    TvDB_Series tvser = TvDBApiHelper.GetSeriesInfoOnline(xref.TvDBID, false);
                                    if (tvser != null)
                                    {
                                        logger.Trace("Found tvdb match on web cache for {0}", AnimeID);
                                        TvDBApiHelper.LinkAniDBTvDB(AnimeID,
                                            (EpisodeType) xref.AniDBStartEpisodeType,
                                            xref.AniDBStartEpisodeNumber,
                                            xref.TvDBID, xref.TvDBSeasonNumber,
                                            xref.TvDBStartEpisodeNumber, true, true);
                                    }
                                }
                                return;
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }

                    if (!ServerSettings.TvDB_AutoLink) return;

                    string searchCriteria = string.Empty;
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
                    if (anime == null) return;

                    searchCriteria = anime.MainTitle;

                    // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
                    List<TVDB_Series_Search_Response> results = TvDBApiHelper.SearchSeries(searchCriteria);
                    logger.Trace("Found {0} tvdb results for {1} on TheTvDB", results.Count, searchCriteria);
                    if (ProcessSearchResults(results, searchCriteria)) return;


                    if (results.Count == 0)
                    {
                        bool foundResult = false;
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
                            if (ProcessSearchResults(results, title.Title)) return;
                        }
                        if (!foundResult) logger.Warn("Unable to find a matching TvDB series for {0}", anime.MainTitle);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TvDBSearchAnime: {0} - {1}", AnimeID, ex);
            }
        }

        private bool ProcessSearchResults(List<TVDB_Series_Search_Response> results, string searchCriteria)
        {
            if (results.Count == 1)
            {
                // since we are using this result, lets download the info
                logger.Trace("Found 1 tvdb results for search on {0} --- Linked to {1} ({2})", searchCriteria,
                    results[0].SeriesName,
                    results[0].SeriesID);
                TvDB_Series tvser = TvDBApiHelper.GetSeriesInfoOnline(results[0].SeriesID, false);
                TvDBApiHelper.LinkAniDBTvDB(AnimeID, EpisodeType.Episode, 1, results[0].SeriesID, 1, 1, true);

                // add links for multiple seasons (for long shows)
                List<int> seasons = RepoFactory.TvDB_Episode.GetSeasonNumbersForSeries(results[0].SeriesID);
                foreach (int season in seasons)
                {
                    if (season < 2) continue; // we just linked season 1, so start after (and skip specials)
                    TvDB_Episode ep = RepoFactory.TvDB_Episode.GetBySeriesIDSeasonNumberAndEpisode(results[0].SeriesID, season, 1);
                    if (ep?.AbsoluteNumber != null)
                    {
                        AddCrossRef_AniDB_TvDBV2(AnimeID, ep.AbsoluteNumber.Value, results[0].SeriesID,
                            season, tvser?.SeriesName ?? string.Empty);
                    }
                }
                SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);
                return true;
            }
            if (results.Count > 1)
            {
                logger.Trace("Found multiple ({0}) tvdb results for search on so checking for english results {1}",
                    results.Count,
                    searchCriteria);
                foreach (TVDB_Series_Search_Response sres in results)
                {
                    // since we are using this result, lets download the info
                    logger.Trace("Found english result for search on {0} --- Linked to {1} ({2})", searchCriteria,
                        sres.SeriesName,
                        sres.SeriesID);
                    TvDB_Series tvser = TvDBApiHelper.GetSeriesInfoOnline(results[0].SeriesID, false);
                    TvDBApiHelper.LinkAniDBTvDB(AnimeID, EpisodeType.Episode, 1, sres.SeriesID, 1, 1, true);

                    // add links for multiple seasons (for long shows)
                    List<int> seasons = RepoFactory.TvDB_Episode.GetSeasonNumbersForSeries(results[0].SeriesID);
                    foreach (int season in seasons)
                    {
                        if (season < 2) continue; // we just linked season 1, so start after (and skip specials)
                        TvDB_Episode ep = RepoFactory.TvDB_Episode
                            .GetBySeriesIDSeasonNumberAndEpisode(results[0].SeriesID, season, 1);
                        if (ep?.AbsoluteNumber != null)
                        {
                            AddCrossRef_AniDB_TvDBV2(AnimeID, ep.AbsoluteNumber.Value, results[0].SeriesID,
                                season, tvser?.SeriesName ?? string.Empty);
                        }
                    }
                    return true;
                }
                logger.Trace("No english results found, so SKIPPING: {0}", searchCriteria);
            }

            return false;
        }

        private static void AddCrossRef_AniDB_TvDBV2(int animeID, int anistart, int tvdbID, int tvdbSeason,
            string title)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                CrossRef_AniDB_TvDBV2 xref =
                    RepoFactory.CrossRef_AniDB_TvDBV2.GetByTvDBID(tvdbID, tvdbSeason, 1, animeID,
                        (int) EpisodeType.Episode, anistart);
                if (xref != null) return;
                xref = new CrossRef_AniDB_TvDBV2
                {
                    AnimeID = animeID,
                    AniDBStartEpisodeType = (int)EpisodeType.Episode,
                    AniDBStartEpisodeNumber = anistart,

                    TvDBID = tvdbID,
                    TvDBSeasonNumber = tvdbSeason,
                    TvDBStartEpisodeNumber = 1,
                    TvDBTitle = title
                };
                RepoFactory.CrossRef_AniDB_TvDBV2.Save(xref);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_TvDBSearchAnime{AnimeID}";
        }

        public override bool InitFromDB(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            CommandType = cq.CommandType;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBSearchAnime", "AnimeID"));
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBSearchAnime", "ForceRefresh"));
            }

            return true;
        }
    }
}