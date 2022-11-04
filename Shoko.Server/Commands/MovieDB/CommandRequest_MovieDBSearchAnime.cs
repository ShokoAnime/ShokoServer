using System;
using System.Collections.Generic;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.MovieDB_SearchAnime)]
public class CommandRequest_MovieDBSearchAnime : CommandRequestImplementation
{
    private readonly MovieDBHelper _helper;
    public int AnimeID { get; set; }
    public bool ForceRefresh { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Searching for anime on The MovieDB: {0}",
        queueState = QueueStateEnum.SearchTMDb,
        extraParams = new[] { AnimeID.ToString() }
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_MovieDBSearchAnime: {AnimeID}", AnimeID);

        try
        {
            // Use TvDB setting
            if (!ServerSettings.Instance.TvDB.AutoLink)
            {
                return;
            }

            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
            if (anime == null)
            {
                return;
            }

            var searchCriteria = anime.PreferredTitle;

            // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
            var results = _helper.Search(searchCriteria);
            Logger.LogTrace("Found {Count} moviedb results for {Criteria} on MovieDB", results.Count, searchCriteria);
            if (ProcessSearchResults(results, searchCriteria))
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

                if (string.Equals(searchCriteria, title.Title, StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }

                results = _helper.Search(title.Title);
                Logger.LogTrace("Found {Count} moviedb results for search on {Title}", results.Count, title.Title);
                if (ProcessSearchResults(results, title.Title))
                {
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error processing CommandRequest_TvDBSearchAnime: {AnimeID} - {Ex}", AnimeID, ex);
        }
    }

    private bool ProcessSearchResults(List<MovieDB_Movie_Result> results, string searchCriteria)
    {
        if (results.Count == 1)
        {
            // since we are using this result, lets download the info
            Logger.LogTrace("Found 1 moviedb results for search on {SearchCriteria} --- Linked to {Name} ({ID})",
                searchCriteria,
                results[0].MovieName, results[0].MovieID);

            var movieID = results[0].MovieID;
            _helper.UpdateMovieInfo(movieID, true);
            _helper.LinkAniDBMovieDB(AnimeID, movieID, false);
            return true;
        }

        return false;
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_MovieDBSearchAnime{AnimeID}";
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
        AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_MovieDBSearchAnime", "AnimeID"));
        ForceRefresh =
            bool.Parse(TryGetProperty(docCreator, "CommandRequest_MovieDBSearchAnime", "ForceRefresh"));

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

    public CommandRequest_MovieDBSearchAnime(ILoggerFactory loggerFactory, MovieDBHelper helper) : base(loggerFactory)
    {
        _helper = helper;
    }

    protected CommandRequest_MovieDBSearchAnime()
    {
    }
}
