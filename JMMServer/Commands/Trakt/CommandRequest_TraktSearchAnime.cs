using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Xml;
using AniDBAPI;
using JMMContracts;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Providers.Azure;
using JMMServer.Providers.TraktTV;
using JMMServer.Providers.TraktTV.Contracts;
using JMMServer.Repositories;
using NHibernate;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_TraktSearchAnime : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_TraktSearchAnime()
        {
        }

        public CommandRequest_TraktSearchAnime(int animeID, bool forced)
        {
            AnimeID = animeID;
            ForceRefresh = forced;
            CommandType = (int)CommandRequestType.Trakt_SearchAnime;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_SearchTrakt, AnimeID);
            }
        }


        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TraktSearchAnime: {0}", AnimeID);

            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    // first check if the user wants to use the web cache
                    if (ServerSettings.WebCache_Trakt_Get)
                    {
                        try
                        {
                            var contracts = new List<Contract_Azure_CrossRef_AniDB_Trakt>();

                            var resultsCache = AzureWebAPI.Get_CrossRefAniDBTrakt(AnimeID);
                            if (resultsCache != null && resultsCache.Count > 0)
                            {
                                foreach (var xref in resultsCache)
                                {
                                    var showInfo = TraktTVHelper.GetShowInfoV2(xref.TraktID);
                                    if (showInfo != null)
                                    {
                                        logger.Trace("Found trakt match on web cache for {0} - id = {1}", AnimeID,
                                            showInfo.title);
                                        TraktTVHelper.LinkAniDBTrakt(AnimeID, (enEpisodeType)xref.AniDBStartEpisodeType,
                                            xref.AniDBStartEpisodeNumber,
                                            xref.TraktID, xref.TraktSeasonNumber, xref.TraktStartEpisodeNumber, true);
                                        return;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.ErrorException(ex.ToString(), ex);
                        }
                    }


                    // lets try to see locally if we have a tvDB link for this anime
                    // Trakt allows the use of TvDB ID's or their own Trakt ID's
                    var repCrossRefTvDB = new CrossRef_AniDB_TvDBV2Repository();
                    var xrefTvDBs = repCrossRefTvDB.GetByAnimeID(session, AnimeID);
                    if (xrefTvDBs != null && xrefTvDBs.Count > 0)
                    {
                        foreach (var tvXRef in xrefTvDBs)
                        {
                            // first search for this show by the TvDB ID
                            var searchResults = TraktTVHelper.SearchShowByIDV2(TraktSearchIDType.tvdb,
                                tvXRef.TvDBID.ToString());
                            if (searchResults != null && searchResults.Count > 0)
                            {
                                // since we are searching by ID, there will only be one 'show' result
                                TraktV2Show resShow = null;
                                foreach (var res in searchResults)
                                {
                                    if (res.ResultType == SearchIDType.Show)
                                    {
                                        resShow = res.show;
                                        break;
                                    }
                                }

                                if (resShow != null)
                                {
                                    var showInfo = TraktTVHelper.GetShowInfoV2(resShow.ids.slug);
                                    if (showInfo != null && showInfo.ids != null)
                                    {
                                        // make sure the season specified by TvDB also exists on Trakt
                                        var repShow = new Trakt_ShowRepository();
                                        var traktShow = repShow.GetByTraktSlug(session, showInfo.ids.slug);
                                        if (traktShow != null)
                                        {
                                            var repSeasons = new Trakt_SeasonRepository();
                                            var traktSeason = repSeasons.GetByShowIDAndSeason(session,
                                                traktShow.Trakt_ShowID, xrefTvDBs[0].TvDBSeasonNumber);
                                            if (traktSeason != null)
                                            {
                                                logger.Trace("Found trakt match using TvDBID locally {0} - id = {1}",
                                                    AnimeID, showInfo.title);
                                                TraktTVHelper.LinkAniDBTrakt(AnimeID,
                                                    (enEpisodeType)tvXRef.AniDBStartEpisodeType,
                                                    tvXRef.AniDBStartEpisodeNumber, showInfo.ids.slug,
                                                    tvXRef.TvDBSeasonNumber, tvXRef.TvDBStartEpisodeNumber, true);
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // finally lets try searching Trakt directly
                    var searchCriteria = "";
                    var repAnime = new AniDB_AnimeRepository();
                    var anime = repAnime.GetByAnimeID(session, AnimeID);
                    if (anime == null) return;

                    searchCriteria = anime.MainTitle;

                    // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
                    var results = TraktTVHelper.SearchShowV2(searchCriteria);
                    logger.Trace("Found {0} trakt results for {1} ", results.Count, searchCriteria);
                    if (ProcessSearchResults(session, results, searchCriteria)) return;


                    if (results.Count == 0)
                    {
                        foreach (var title in anime.GetTitles(session))
                        {
                            if (title.TitleType.ToUpper() != Constants.AnimeTitleType.Official.ToUpper()) continue;

                            if (searchCriteria.ToUpper() == title.Title.ToUpper()) continue;

                            results = TraktTVHelper.SearchShowV2(searchCriteria);
                            logger.Trace("Found {0} trakt results for search on {1}", results.Count, title.Title);
                            if (ProcessSearchResults(session, results, title.Title)) return;
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSearchAnime", "AnimeID"));
                ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSearchAnime", "ForceRefresh"));
            }

            return true;
        }

        private bool ProcessSearchResults(ISession session, List<TraktV2SearchShowResult> results, string searchCriteria)
        {
            if (results.Count == 1)
            {
                if (results[0].show != null)
                {
                    // since we are using this result, lets download the info
                    logger.Trace("Found 1 trakt results for search on {0} --- Linked to {1} ({2})", searchCriteria,
                        results[0].show.Title, results[0].show.ids.slug);
                    var showInfo = TraktTVHelper.GetShowInfoV2(results[0].show.ids.slug);
                    if (showInfo != null)
                    {
                        TraktTVHelper.LinkAniDBTrakt(session, AnimeID, enEpisodeType.Episode, 1,
                            results[0].show.ids.slug, 1, 1, true);
                        return true;
                    }
                }
            }

            return false;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_TraktSearchAnime{0}", AnimeID);
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