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
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.TMDB_Movie_Update)]
public class CommandRequest_TMDB_Movie_Update : CommandRequestImplementation
{
    [XmlIgnore, JsonIgnore]
    private readonly TMDBHelper _helper;

    [XmlIgnore, JsonIgnore]
    private readonly ISettingsProvider _settingsProvider;

    public virtual int TmdbMovieID { get; set; }

    public virtual bool DownloadImages { get; set; }

    public virtual bool? DownloadCollections { get; set; }

    public virtual bool ForceRefresh { get; set; }

    public virtual string MovieTitle { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

    public override QueueStateStruct PrettyDescription => string.IsNullOrEmpty(MovieTitle) ?
        new()
        {
            message = "Download TMDB Movie: {0}",
            queueState = QueueStateEnum.GettingTvDBSeries,
            extraParams = new[] { TmdbMovieID.ToString() }
        } :
        new()
        {
            message = "Update TMDB Movie: {0}",
            queueState = QueueStateEnum.GettingTvDBSeries,
            extraParams = new[] { $"{MovieTitle} ({TmdbMovieID})" }
        };

    public override void PostInit()
    {
        MovieTitle ??= RepoFactory.MovieDb_Movie.GetByOnlineID(TmdbMovieID)?.MovieName;
    }

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_TMDB_Movie_Update: {TmdbMovieId}", TmdbMovieID);
        Task.Run(() => _helper.UpdateMovie(TmdbMovieID, ForceRefresh, DownloadImages, DownloadCollections ?? _settingsProvider.GetSettings().TMDB.AutoDownloadCollections))
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_TMDB_Movie_Update_{TmdbMovieID}";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        TmdbMovieID = int.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Movie_Update), nameof(TmdbMovieID)));
        ForceRefresh = bool.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Movie_Update), nameof(ForceRefresh)));
        DownloadImages = bool.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Movie_Update), nameof(DownloadImages)));
        DownloadCollections = bool.TryParse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Movie_Update), nameof(DownloadCollections)), out var value) ? value : null;
        MovieTitle = docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Movie_Update), nameof(MovieTitle));

        return true;
    }

    public CommandRequest_TMDB_Movie_Update(ILoggerFactory loggerFactory, TMDBHelper helper, ISettingsProvider settingsProvider) : base(loggerFactory)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_TMDB_Movie_Update()
    {
    }
}
