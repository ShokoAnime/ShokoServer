using System;
using System.Collections.Generic;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.MovieDB_SearchAnime)]
public class CommandRequest_MovieDBSearchAnime : CommandRequestImplementation
{
    private readonly MovieDBHelper _helper;
    private readonly ISettingsProvider _settingsProvider;
    public virtual int AnimeID { get; set; }
    public virtual bool ForceRefresh { get; set; }

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

        // Use TvDB setting
        var settings = _settingsProvider.GetSettings();
        if (!settings.TvDB.AutoLink)
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
            if (title.TitleType != TitleType.Official)
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

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        AnimeID = int.Parse(docCreator.TryGetProperty("CommandRequest_MovieDBSearchAnime", "AnimeID"));
        ForceRefresh =
            bool.Parse(docCreator.TryGetProperty("CommandRequest_MovieDBSearchAnime", "ForceRefresh"));

        return true;
    }

    public CommandRequest_MovieDBSearchAnime(ILoggerFactory loggerFactory, MovieDBHelper helper, ISettingsProvider settingsProvider) : base(loggerFactory)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_MovieDBSearchAnime()
    {
    }
}
