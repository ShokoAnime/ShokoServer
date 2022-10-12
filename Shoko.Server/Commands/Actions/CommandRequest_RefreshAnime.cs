﻿using System;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Server;

namespace Shoko.Server.Commands;

[Command(CommandRequestType.Refresh_AnimeStats)]
public class CommandRequest_RefreshAnime : CommandRequestImplementation
{
    public int AnimeID { get; set; }


    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority8;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Refreshing anime stats: {0}",
        queueState = QueueStateEnum.Refresh,
        extraParams = new[] { AnimeID.ToString() }
    };

    protected override void Process()
    {
        SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_RefreshAnime_{AnimeID}";
    }

    public override bool LoadFromDBCommand(CommandRequest cq)
    {
        CommandID = cq.CommandID;
        CommandRequestID = cq.CommandRequestID;
        Priority = cq.Priority;
        CommandDetails = cq.CommandDetails;
        DateTimeUpdated = cq.DateTimeUpdated;
        AnimeID = int.Parse(cq.CommandDetails);
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
            CommandDetails = AnimeID.ToString(),
            DateTimeUpdated = DateTime.Now
        };
        return cq;
    }

    public CommandRequest_RefreshAnime(ILoggerFactory loggerFactory) : base(loggerFactory)
    {
    }

    protected CommandRequest_RefreshAnime()
    {
    }
}
