﻿using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.Trakt_EpisodeCollection)]
public class CommandRequest_TraktCollectionEpisode : CommandRequestImplementation
{
    private readonly TraktTVHelper _helper;
    public int AnimeEpisodeID { get; set; }
    public int Action { get; set; }

    public TraktSyncAction ActionEnum => (TraktSyncAction)Action;

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority9;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Sync episode to collection on Trakt: {0} - {1}",
        queueState = QueueStateEnum.SyncTraktEpisodes,
        extraParams = new[] { AnimeEpisodeID.ToString(), Action.ToString() }
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_TraktCollectionEpisode: {0}-{1}", AnimeEpisodeID, Action);

        try
        {
            if (!ServerSettings.Instance.TraktTv.Enabled ||
                string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
            {
                return;
            }

            var ep = RepoFactory.AnimeEpisode.GetByID(AnimeEpisodeID);
            if (ep != null)
            {
                var syncType = TraktSyncType.CollectionAdd;
                if (ActionEnum == TraktSyncAction.Remove)
                {
                    syncType = TraktSyncType.CollectionRemove;
                }

                _helper.SyncEpisodeToTrakt(ep, syncType);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error processing CommandRequest_TraktCollectionEpisode: {0} - {1} - {2}", AnimeEpisodeID,
                Action,
                ex);
        }
    }

    /// <summary>
    /// This should generate a unique key for a command
    /// It will be used to check whether the command has already been queued before adding it
    /// </summary>
    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_TraktCollectionEpisode{AnimeEpisodeID}-{Action}";
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
            var docCreator = new XmlDocument();
            docCreator.LoadXml(CommandDetails);

            // populate the fields
            AnimeEpisodeID =
                int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktCollectionEpisode", "AnimeEpisodeID"));
            Action = int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktCollectionEpisode", "Action"));
        }

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

    public CommandRequest_TraktCollectionEpisode(ILoggerFactory loggerFactory, TraktTVHelper helper) : base(
        loggerFactory)
    {
        _helper = helper;
    }

    protected CommandRequest_TraktCollectionEpisode()
    {
    }
}
