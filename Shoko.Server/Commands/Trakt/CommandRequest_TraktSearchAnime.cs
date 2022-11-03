using System;
using System.Collections.Generic;
using System.Xml;
using Microsoft.Extensions.Logging;
using NHibernate;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Databases;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TraktTV.Contracts;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.Trakt_SearchAnime)]
public class CommandRequest_TraktSearchAnime : CommandRequestImplementation
{
    private readonly TraktTVHelper _helper;
    public int AnimeID { get; set; }
    public bool ForceRefresh { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Searching for anime on Trakt.TV: {0}",
        queueState = QueueStateEnum.SearchTrakt,
        extraParams = new[] { AnimeID.ToString() }
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_TraktSearchAnime: {0}", AnimeID);

        try
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var sessionWrapper = session.Wrap();
                var doReturn = false;

                // lets try to see locally if we have a tvDB link for this anime
                // Trakt allows the use of TvDB ID's or their own Trakt ID's
                var
                    xrefTvDBs = RepoFactory.CrossRef_AniDB_TvDB.GetV2LinksFromAnime(AnimeID);
                if (xrefTvDBs != null && xrefTvDBs.Count > 0)
                {
                    foreach (var tvXRef in xrefTvDBs)
                    {
                        // first search for this show by the TvDB ID
                        var searchResults =
                            _helper.SearchShowByIDV2(TraktSearchIDType.tvdb,
                                tvXRef.TvDBID.ToString());
                        if (searchResults == null || searchResults.Count <= 0)
                        {
                            continue;
                        }

                        // since we are searching by ID, there will only be one 'show' result
                        TraktV2Show resShow = null;
                        foreach (var res in searchResults)
                        {
                            if (res.ResultType != SearchIDType.Show)
                            {
                                continue;
                            }

                            resShow = res.show;
                            break;
                        }

                        if (resShow == null)
                        {
                            continue;
                        }

                        var showInfo = _helper.GetShowInfoV2(resShow.ids.slug);
                        if (showInfo?.ids == null)
                        {
                            continue;
                        }

                        // make sure the season specified by TvDB also exists on Trakt
                        var traktShow =
                            RepoFactory.Trakt_Show.GetByTraktSlug(session, showInfo.ids.slug);
                        if (traktShow == null)
                        {
                            continue;
                        }

                        var traktSeason = RepoFactory.Trakt_Season.GetByShowIDAndSeason(
                            session,
                            traktShow.Trakt_ShowID,
                            tvXRef.TvDBSeasonNumber);
                        if (traktSeason == null)
                        {
                            continue;
                        }

                        Logger.LogTrace("Found trakt match using TvDBID locally {0} - id = {1}",
                            AnimeID, showInfo.title);
                        _helper.LinkAniDBTrakt(AnimeID,
                            (EpisodeType)tvXRef.AniDBStartEpisodeType,
                            tvXRef.AniDBStartEpisodeNumber, showInfo.ids.slug,
                            tvXRef.TvDBSeasonNumber, tvXRef.TvDBStartEpisodeNumber,
                            true);
                        doReturn = true;
                    }

                    if (doReturn)
                    {
                        return;
                    }
                }

                // Use TvDB setting due to similarity
                if (!ServerSettings.Instance.TvDB.AutoLink)
                {
                    return;
                }

                // finally lets try searching Trakt directly
                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, AnimeID);
                if (anime == null)
                {
                    return;
                }

                var searchCriteria = anime.MainTitle;

                // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
                var results = _helper.SearchShowV2(searchCriteria);
                Logger.LogTrace("Found {0} trakt results for {1} ", results.Count, searchCriteria);
                if (ProcessSearchResults(session, results, searchCriteria))
                {
                    return;
                }


                if (results.Count != 0)
                {
                    return;
                }

                foreach (var title in anime.GetTitles())
                {
                    if (title.TitleType != Shoko.Plugin.Abstractions.DataModels.TitleType.Official)
                    {
                        continue;
                    }

                    if (string.Equals(searchCriteria, title.Title, StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    results = _helper.SearchShowV2(searchCriteria);
                    Logger.LogTrace("Found {0} trakt results for search on {1}", results.Count, title.Title);
                    if (ProcessSearchResults(session, results, title.Title))
                    {
                        return;
                    }
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
                var showInfo = _helper.GetShowInfoV2(results[0].show.ids.slug);
                if (showInfo != null)
                {
                    _helper.LinkAniDBTrakt(session, AnimeID, EpisodeType.Episode, 1,
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
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSearchAnime", "AnimeID"));
        ForceRefresh =
            bool.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSearchAnime", "ForceRefresh"));

        return true;
    }

    public override CommandRequest ToDatabaseObject()
    {
        GenerateCommandID();

        var cq = new CommandRequest
        {
            CommandID = CommandID,
            CommandType = CommandType,
            Priority = Priority,
            CommandDetails = ToXML(),
            DateTimeUpdated = DateTime.Now
        };
        return cq;
    }

    public CommandRequest_TraktSearchAnime(ILoggerFactory loggerFactory, TraktTVHelper helper) : base(loggerFactory)
    {
        _helper = helper;
    }

    protected CommandRequest_TraktSearchAnime()
    {
    }
}
