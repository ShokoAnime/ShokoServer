using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Xml;
using AniDBAPI;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Providers.Azure;
using JMMServer.Providers.TvDB;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_TvDBSearchAnime : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_TvDBSearchAnime()
        {
        }

        public CommandRequest_TvDBSearchAnime(int animeID, bool forced)
        {
            AnimeID = animeID;
            ForceRefresh = forced;
            CommandType = (int)CommandRequestType.TvDB_SearchAnime;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority8; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_SearchTvDB, AnimeID);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TvDBSearchAnime: {0}", AnimeID);

            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    // first check if the user wants to use the web cache
                    if (ServerSettings.WebCache_TvDB_Get)
                    {
                        try
                        {
                            var cacheResults = AzureWebAPI.Get_CrossRefAniDBTvDB(AnimeID);
                            if (cacheResults != null && cacheResults.Count > 0)
                            {
                                // check again to see if there are any links, user may have manually added links while
                                // this command was in the queue
                                var repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
                                var xrefTemp = repCrossRef.GetByAnimeID(AnimeID);
                                if (xrefTemp != null && xrefTemp.Count > 0) return;

                                foreach (var xref in cacheResults)
                                {
                                    var tvser = TvDBHelper.GetSeriesInfoOnline(cacheResults[0].TvDBID);
                                    if (tvser != null)
                                    {
                                        logger.Trace("Found tvdb match on web cache for {0}", AnimeID);
                                        TvDBHelper.LinkAniDBTvDB(AnimeID,
                                            (enEpisodeType)cacheResults[0].AniDBStartEpisodeType,
                                            cacheResults[0].AniDBStartEpisodeNumber,
                                            cacheResults[0].TvDBID, cacheResults[0].TvDBSeasonNumber,
                                            cacheResults[0].TvDBStartEpisodeNumber, true);
                                    }
                                }
                                return;
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }

                    var searchCriteria = "";
                    var repAnime = new AniDB_AnimeRepository();
                    var anime = repAnime.GetByAnimeID(AnimeID);
                    if (anime == null) return;

                    searchCriteria = anime.MainTitle;

                    // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
                    var results = JMMService.TvdbHelper.SearchSeries(searchCriteria);
                    logger.Trace("Found {0} tvdb results for {1} on TheTvDB", results.Count, searchCriteria);
                    if (ProcessSearchResults(results, searchCriteria)) return;


                    if (results.Count == 0)
                    {
                        foreach (var title in anime.GetTitles())
                        {
                            if (title.TitleType.ToUpper() != Constants.AnimeTitleType.Official.ToUpper()) continue;

                            if (searchCriteria.ToUpper() == title.Title.ToUpper()) continue;

                            results = JMMService.TvdbHelper.SearchSeries(title.Title);
                            logger.Trace("Found {0} tvdb results for search on {1}", results.Count, title.Title);
                            if (ProcessSearchResults(results, title.Title)) return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TvDBSearchAnime: {0} - {1}", AnimeID, ex.ToString());
            }
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
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
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBSearchAnime", "AnimeID"));
                ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBSearchAnime", "ForceRefresh"));
            }

            return true;
        }

        private bool ProcessSearchResults(List<TVDBSeriesSearchResult> results, string searchCriteria)
        {
            if (results.Count == 1)
            {
                // since we are using this result, lets download the info
                logger.Trace("Found 1 tvdb results for search on {0} --- Linked to {1} ({2})", searchCriteria,
                    results[0].SeriesName, results[0].SeriesID);
                var tvser = TvDBHelper.GetSeriesInfoOnline(results[0].SeriesID);
                TvDBHelper.LinkAniDBTvDB(AnimeID, enEpisodeType.Episode, 1, results[0].SeriesID, 1, 1, true);
                return true;
            }
            if (results.Count > 1)
            {
                logger.Trace("Found multiple ({0}) tvdb results for search on so checking for english results {1}",
                    results.Count, searchCriteria);
                foreach (var sres in results)
                {
                    if (sres.Language.Equals("en", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // since we are using this result, lets download the info
                        logger.Trace("Found english result for search on {0} --- Linked to {1} ({2})", searchCriteria,
                            sres.SeriesName, sres.SeriesID);
                        var tvser = TvDBHelper.GetSeriesInfoOnline(results[0].SeriesID);
                        TvDBHelper.LinkAniDBTvDB(AnimeID, enEpisodeType.Episode, 1, sres.SeriesID, 1, 1, true);
                        return true;
                    }
                }
                logger.Trace("No english results found, so SKIPPING: {0}", searchCriteria);
            }

            return false;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_TvDBSearchAnime{0}", AnimeID);
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest();
            cq.CommandID = CommandID;
            cq.CommandType = CommandType;
            cq.Priority = Priority;
            cq.CommandDetails = ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}