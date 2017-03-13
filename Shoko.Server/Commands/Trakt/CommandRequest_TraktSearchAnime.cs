using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Xml;
using AniDBAPI;
using Shoko.Models;
using Shoko.Server.Repositories.Direct;
using NHibernate;
using Shoko.Models.Azure;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TraktTV.Contracts;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_TraktSearchAnime : CommandRequestImplementation, ICommandRequest
    {
        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct()
                {
                    queueState = QueueStateEnum.SearchTrakt,
                    extraParams = new string[] {AnimeID.ToString()}
                };
            }
        }

        public CommandRequest_TraktSearchAnime()
        {
        }

        public CommandRequest_TraktSearchAnime(int animeID, bool forced)
        {
            this.AnimeID = animeID;
            this.ForceRefresh = forced;
            this.CommandType = (int) CommandRequestType.Trakt_SearchAnime;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }


        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TraktSearchAnime: {0}", AnimeID);

            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();

                    // first check if the user wants to use the web cache
                    if (ServerSettings.WebCache_Trakt_Get)
                    {
                        try
                        {
                            List<Azure_CrossRef_AniDB_Trakt> contracts =
                                new List<Azure_CrossRef_AniDB_Trakt>();

                            List<Azure_CrossRef_AniDB_Trakt> resultsCache =
                                AzureWebAPI.Get_CrossRefAniDBTrakt(AnimeID);
                            if (resultsCache != null && resultsCache.Count > 0)
                            {
                                foreach (Azure_CrossRef_AniDB_Trakt xref in resultsCache)
                                {
                                    TraktV2ShowExtended showInfo = TraktTVHelper.GetShowInfoV2(xref.TraktID);
                                    if (showInfo != null)
                                    {
                                        logger.Trace("Found trakt match on web cache for {0} - id = {1}", AnimeID,
                                            showInfo.title);
                                        TraktTVHelper.LinkAniDBTrakt(AnimeID,
                                            (enEpisodeType) xref.AniDBStartEpisodeType,
                                            xref.AniDBStartEpisodeNumber,
                                            xref.TraktID, xref.TraktSeasonNumber, xref.TraktStartEpisodeNumber, true);
                                        return;
                                    }
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
                    List<CrossRef_AniDB_TvDBV2> xrefTvDBs =
                        RepoFactory.CrossRef_AniDB_TvDBV2.GetByAnimeID(sessionWrapper, AnimeID);
                    if (xrefTvDBs != null && xrefTvDBs.Count > 0)
                    {
                        foreach (CrossRef_AniDB_TvDBV2 tvXRef in xrefTvDBs)
                        {
                            // first search for this show by the TvDB ID
                            List<TraktV2SearchTvDBIDShowResult> searchResults =
                                TraktTVHelper.SearchShowByIDV2(TraktSearchIDType.tvdb,
                                    tvXRef.TvDBID.ToString());
                            if (searchResults != null && searchResults.Count > 0)
                            {
                                // since we are searching by ID, there will only be one 'show' result
                                TraktV2Show resShow = null;
                                foreach (TraktV2SearchTvDBIDShowResult res in searchResults)
                                {
                                    if (res.ResultType == SearchIDType.Show)
                                    {
                                        resShow = res.show;
                                        break;
                                    }
                                }

                                if (resShow != null)
                                {
                                    TraktV2ShowExtended showInfo = TraktTVHelper.GetShowInfoV2(resShow.ids.slug);
                                    if (showInfo != null && showInfo.ids != null)
                                    {
                                        // make sure the season specified by TvDB also exists on Trakt
                                        Trakt_Show traktShow =
                                            RepoFactory.Trakt_Show.GetByTraktSlug(session, showInfo.ids.slug);
                                        if (traktShow != null)
                                        {
                                            Trakt_Season traktSeason = RepoFactory.Trakt_Season.GetByShowIDAndSeason(
                                                session,
                                                traktShow.Trakt_ShowID,
                                                xrefTvDBs[0].TvDBSeasonNumber);
                                            if (traktSeason != null)
                                            {
                                                logger.Trace("Found trakt match using TvDBID locally {0} - id = {1}",
                                                    AnimeID, showInfo.title);
                                                TraktTVHelper.LinkAniDBTrakt(AnimeID,
                                                    (enEpisodeType) tvXRef.AniDBStartEpisodeType,
                                                    tvXRef.AniDBStartEpisodeNumber, showInfo.ids.slug,
                                                    tvXRef.TvDBSeasonNumber, tvXRef.TvDBStartEpisodeNumber,
                                                    true);
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // finally lets try searching Trakt directly
                    string searchCriteria = "";
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, AnimeID);
                    if (anime == null) return;

                    searchCriteria = anime.MainTitle;

                    // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
                    List<TraktV2SearchShowResult> results = TraktTVHelper.SearchShowV2(searchCriteria);
                    logger.Trace("Found {0} trakt results for {1} ", results.Count, searchCriteria);
                    if (ProcessSearchResults(session, results, searchCriteria)) return;


                    if (results.Count == 0)
                    {
                        foreach (AniDB_Anime_Title title in anime.GetTitles())
                        {
                            if (title.TitleType.ToUpper() != Shoko.Models.Constants.AnimeTitleType.Official.ToUpper())
                                continue;

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
                return;
            }
        }

        private bool ProcessSearchResults(ISession session, List<TraktV2SearchShowResult> results,
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
                        TraktTVHelper.LinkAniDBTrakt(session, AnimeID, enEpisodeType.Episode, 1,
                            results[0].show.ids.slug, 1, 1,
                            true);
                        return true;
                    }
                }
            }

            return false;
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_TraktSearchAnime{0}", this.AnimeID);
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            this.CommandID = cq.CommandID;
            this.CommandRequestID = cq.CommandRequestID;
            this.CommandType = cq.CommandType;
            this.Priority = cq.Priority;
            this.CommandDetails = cq.CommandDetails;
            this.DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (this.CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(this.CommandDetails);

                // populate the fields
                this.AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSearchAnime", "AnimeID"));
                this.ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSearchAnime", "ForceRefresh"));
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest();
            cq.CommandID = this.CommandID;
            cq.CommandType = this.CommandType;
            cq.Priority = this.Priority;
            cq.CommandDetails = this.ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}