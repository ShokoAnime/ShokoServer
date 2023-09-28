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
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.TMDB_Show_Update)]
public class CommandRequest_TMDB_Show_Update : CommandRequestImplementation
{
    [XmlIgnore, JsonIgnore]
    private readonly TMDBHelper _helper;

    [XmlIgnore, JsonIgnore]
    private readonly ISettingsProvider _settingsProvider;

    public virtual int TmdbShowID { get; set; }

    public virtual bool DownloadImages { get; set; }

    public virtual bool? DownloadEpisodeGroups { get; set; }

    public virtual bool ForceRefresh { get; set; }

    public virtual string ShowTitle { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

    public override QueueStateStruct PrettyDescription => string.IsNullOrEmpty(ShowTitle) ?
        new()
        {
            message = "Download TMDB Show: {0}",
            queueState = QueueStateEnum.GettingTvDBSeries,
            extraParams = new[] { TmdbShowID.ToString() }
        } :
        new()
        {
            message = "Update TMDB Show: {0}",
            queueState = QueueStateEnum.GettingTvDBSeries,
            extraParams = new[] { $"{ShowTitle} ({TmdbShowID})" }
        };

    public override void PostInit()
    {
        // TODO: Set the show title when we have finalised the show model and the repostory is usable.
        ShowTitle ??= null;
    }

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_TMDB_Show_Update: {TmdbShowId}", TmdbShowID);
        Task.Run(async () => await _helper.UpdateShow(TmdbShowID, ForceRefresh, DownloadImages, DownloadEpisodeGroups ?? _settingsProvider.GetSettings().TMDB.AutoDownloadEpisodeGroups))
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
        ;
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_TMDB_Show_Update_{TmdbShowID}";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        TmdbShowID = int.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Show_Update), nameof(TmdbShowID)));
        ForceRefresh = bool.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Show_Update), nameof(ForceRefresh)));
        DownloadImages = bool.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Show_Update), nameof(DownloadImages)));
        DownloadEpisodeGroups = bool.TryParse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Show_Update), nameof(DownloadEpisodeGroups)), out var value) ? value : null;
        ShowTitle = docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Show_Update), nameof(ShowTitle));

        return true;
    }

    public CommandRequest_TMDB_Show_Update(ILoggerFactory loggerFactory, TMDBHelper helper, ISettingsProvider settingsProvider) : base(loggerFactory)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_TMDB_Show_Update()
    {
    }
}
