using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.Trakt_EpisodeHistory)]
public class CommandRequest_TraktHistoryEpisode : CommandRequestImplementation
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly TraktTVHelper _helper;
    public virtual int AnimeEpisodeID { get; set; }
    public virtual int Action { get; set; }

    public virtual TraktSyncAction ActionEnum => (TraktSyncAction)Action;

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority9;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Add episode to history on Trakt: {0}",
        queueState = QueueStateEnum.TraktAddHistory,
        extraParams = new[] { AnimeEpisodeID.ToString() }
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_TraktHistoryEpisode: {0}-{1}", AnimeEpisodeID, Action);

        var settings = _settingsProvider.GetSettings();

        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken)) return;

        var ep = RepoFactory.AnimeEpisode.GetByID(AnimeEpisodeID);
        if (ep != null)
        {
            var syncType = TraktSyncType.HistoryAdd;
            if (ActionEnum == TraktSyncAction.Remove)
            {
                syncType = TraktSyncType.HistoryRemove;
            }

            _helper.SyncEpisodeToTrakt(ep, syncType);
        }
    }

    /// <summary>
    /// This should generate a unique key for a command
    /// It will be used to check whether the command has already been queued before adding it
    /// </summary>
    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_TraktHistoryEpisode{AnimeEpisodeID}-{Action}";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        AnimeEpisodeID =
            int.Parse(docCreator.TryGetProperty("CommandRequest_TraktHistoryEpisode", "AnimeEpisodeID"));
        Action = int.Parse(docCreator.TryGetProperty("CommandRequest_TraktHistoryEpisode", "Action"));

        return true;
    }

    public CommandRequest_TraktHistoryEpisode(ILoggerFactory loggerFactory, TraktTVHelper helper, ISettingsProvider settingsProvider) : base(loggerFactory)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_TraktHistoryEpisode()
    {
    }
}
