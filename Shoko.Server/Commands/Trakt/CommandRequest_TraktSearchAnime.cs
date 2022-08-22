using System;
using System.Collections.Generic;
using System.Xml;
using Microsoft.Extensions.Logging;
using NHibernate;
using Shoko.Commons.Queue;
using Shoko.Models.Azure;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TraktTV.Contracts;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands
{
    [Serializable]
    [Command(CommandRequestType.Trakt_SearchAnime)]
    public class CommandRequest_TraktSearchAnime : CommandRequestImplementation
    {
        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            message = "Searching for anime on Trakt.TV: {0}",
            queueState = QueueStateEnum.SearchTrakt,
            extraParams = new[] {AnimeID.ToString()}
        };

        public CommandRequest_TraktSearchAnime()
        {
        }

        public CommandRequest_TraktSearchAnime(int animeID, bool forced)
        {
            AnimeID = animeID;
            ForceRefresh = forced;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }


        protected override void Process(IServiceProvider serviceProvider)
        {
            Logger.LogInformation("Processing CommandRequest_TraktSearchAnime: {0}", AnimeID);

            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();
                    bool doReturn = false;

                    // first check if the user wants to use the web cache
                    if (ServerSettings.Instance.WebCache.Enabled && ServerSettings.Instance.WebCache.Trakt_Get)
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
                                    if (showInfo == null) continue;

                                    Logger.LogTrace("Found trakt match on web cache for {0} - id = {1}", AnimeID,
                                        showInfo.title);
                                    TraktTVHelper.LinkAniDBTrakt(AnimeID,
                                        (EpisodeType) xref.AniDBStartEpisodeType,
                                        xref.AniDBStartEpisodeNumber,
                                        xref.TraktID, xref.TraktSeasonNumber, xref.TraktStartEpisodeNumber, true);
                                    doReturn = true;
                                }
                                if (doReturn) return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, ex.ToString());
                        }
                    }


                    // lets try to see locally if we have a tvDB link for this anime
                    // Trakt allows the use of TvDB ID's or their own Trakt ID's
                    List<CrossRef_AniDB_TvDBV2>
                        xrefTvDBs = RepoFactory.CrossRef_AniDB_TvDB.GetV2LinksFromAnime(AnimeID);
                    if (xrefTvDBs != null && xrefTvDBs.Count > 0)
                    {
                        foreach (CrossRef_AniDB_TvDBV2 tvXRef in xrefTvDBs)
                        {
                            // first search for this show by the TvDB ID
                            List<TraktV2SearchTvDBIDShowResult> searchResults =
                                TraktTVHelper.SearchShowByIDV2(TraktSearchIDType.tvdb,
                                    tvXRef.TvDBID.ToString());
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
                                RepoFactory.Trakt_Show.GetByTraktSlug(session, showInfo.ids.slug);
                            if (traktShow == null) continue;

                            Trakt_Season traktSeason = RepoFactory.Trakt_Season.GetByShowIDAndSeason(
                                session,
                                traktShow.Trakt_ShowID,
                                tvXRef.TvDBSeasonNumber);
                            if (traktSeason == null) continue;

                            Logger.LogTrace("Found trakt match using TvDBID locally {0} - id = {1}",
                                AnimeID, showInfo.title);
                            TraktTVHelper.LinkAniDBTrakt(AnimeID,
                                (EpisodeType) tvXRef.AniDBStartEpisodeType,
                                tvXRef.AniDBStartEpisodeNumber, showInfo.ids.slug,
                                tvXRef.TvDBSeasonNumber, tvXRef.TvDBStartEpisodeNumber,
                                true);
                            doReturn = true;
                        }
                        if (doReturn) return;
                    }

                    // Use TvDB setting due to similarity
                    if (!ServerSettings.Instance.TvDB.AutoLink) return;

                    // finally lets try searching Trakt directly
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, AnimeID);
                    if (anime == null) return;

                    var searchCriteria = anime.MainTitle;

                    // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
                    List<TraktV2SearchShowResult> results = TraktTVHelper.SearchShowV2(searchCriteria);
                    Logger.LogTrace("Found {0} trakt results for {1} ", results.Count, searchCriteria);
                    if (ProcessSearchResults(session, results, searchCriteria)) return;


                    if (results.Count != 0) return;

                    foreach (var title in anime.GetTitles())
                    {
                        if (title.TitleType != Shoko.Plugin.Abstractions.DataModels.TitleType.Official)
                            continue;

                        if (string.Equals(searchCriteria, title.Title, StringComparison.InvariantCultureIgnoreCase)) continue;

                        results = TraktTVHelper.SearchShowV2(searchCriteria);
                        Logger.LogTrace("Found {0} trakt results for search on {1}", results.Count, title.Title);
                        if (ProcessSearchResults(session, results, title.Title)) return;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error processing CommandRequest_TvDBSearchAnime: {0} - {1}", AnimeID, ex);
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
                    Logger.LogTrace("Found 1 trakt results for search on {0} --- Linked to {1} ({2})", searchCriteria,
                        results[0].show.Title, results[0].show.ids.slug);
                    TraktV2ShowExtended showInfo = TraktTVHelper.GetShowInfoV2(results[0].show.ids.slug);
                    if (showInfo != null)
                    {
                        TraktTVHelper.LinkAniDBTrakt(session, AnimeID, EpisodeType.Episode, 1,
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
            CommandID = $"CommandRequest_TraktSearchAnime{AnimeID}";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSearchAnime", "AnimeID"));
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSearchAnime", "ForceRefresh"));
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest
            {
                CommandID = CommandID,
                CommandType = CommandType,
                Priority = Priority,
                CommandDetails = ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}
