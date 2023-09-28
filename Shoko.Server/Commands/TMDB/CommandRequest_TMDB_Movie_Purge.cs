using System;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.TMDB_Movie_Purge)]
public class CommandRequest_TMDB_Movie_Purge : CommandRequestImplementation
{
    [XmlIgnore, JsonIgnore]
    private readonly TMDBHelper _helper;

    public virtual int TmdbMovieID { get; set; }

    public virtual bool RemoveImageFiles { get; set; } = true;

    public virtual string MovieTitle { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

    public override QueueStateStruct PrettyDescription =>
        new()
        {
            message = "Purge TMDB Movie: {0}",
            queueState = QueueStateEnum.GettingTvDBSeries,
            extraParams = new[] { string.IsNullOrEmpty(MovieTitle) ? TmdbMovieID.ToString() : $"{MovieTitle} ({TmdbMovieID})" }
        };

    public override void PostInit()
    {
        MovieTitle ??= RepoFactory.MovieDb_Movie.GetByOnlineID(TmdbMovieID)?.MovieName;
    }

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_TMDB_Movie_Purge: {TmdbMovieId}", TmdbMovieID);
        Task.Run(() => _helper.PurgeMovie(TmdbMovieID, RemoveImageFiles))
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_TMDB_Movie_Purge_{TmdbMovieID}";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        TmdbMovieID = int.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Movie_Purge), nameof(TmdbMovieID)));
        RemoveImageFiles = bool.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Movie_Purge), nameof(RemoveImageFiles)));
        MovieTitle = docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Movie_Purge), nameof(MovieTitle));

        return true;
    }

    public CommandRequest_TMDB_Movie_Purge(ILoggerFactory loggerFactory, TMDBHelper helper) : base(loggerFactory)
    {
        _helper = helper;
    }

    protected CommandRequest_TMDB_Movie_Purge()
    {
    }
}
