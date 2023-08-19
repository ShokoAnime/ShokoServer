using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.Trakt_SyncCollection)]
public class CommandRequest_TraktSyncCollection : CommandRequestImplementation
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly TraktTVHelper _helper;
    public virtual bool ForceRefresh { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority8;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Syncing Trakt collection", queueState = QueueStateEnum.SyncTrakt, extraParams = Array.Empty<string>()
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_TraktSyncCollection");

        var settings = _settingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken)) return;

        var sched =
            RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TraktSync);
        if (sched == null)
        {
            sched = new ScheduledUpdate
            {
                UpdateType = (int)ScheduledUpdateType.TraktSync, UpdateDetails = string.Empty
            };
        }
        else
        {
            var freqHours = Utils.GetScheduledHours(settings.TraktTv.SyncFrequency);

            // if we have run this in the last xxx hours then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
            if (tsLastRun.TotalHours < freqHours && !ForceRefresh) return;
        }

        sched.LastUpdate = DateTime.Now;
        RepoFactory.ScheduledUpdate.Save(sched);

        _helper.SyncCollectionToTrakt();
    }

    /// <summary>
    /// This should generate a unique key for a command
    /// It will be used to check whether the command has already been queued before adding it
    /// </summary>
    public override void GenerateCommandID()
    {
        CommandID = "CommandRequest_TraktSyncCollection";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        ForceRefresh =
            bool.Parse(docCreator.TryGetProperty("CommandRequest_TraktSyncCollection", "ForceRefresh"));

        return true;
    }

    public CommandRequest_TraktSyncCollection(ILoggerFactory loggerFactory, TraktTVHelper helper, ISettingsProvider settingsProvider) : base(loggerFactory)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_TraktSyncCollection()
    {
    }
}
