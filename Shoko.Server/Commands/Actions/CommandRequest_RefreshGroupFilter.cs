﻿using System;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Commands;

[Command(CommandRequestType.Refresh_GroupFilter)]
public class CommandRequest_RefreshGroupFilter : CommandRequestImplementation
{
    public int GroupFilterID { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority9;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Refreshing Group Filter: {0}",
        queueState = QueueStateEnum.RefreshGroupFilter,
        extraParams = new[] { GroupFilterID.ToString() }
    };

    protected override void Process()
    {
        if (GroupFilterID == 0)
        {
            RepoFactory.GroupFilter.CreateOrVerifyLockedFilters();
            return;
        }

        var gf = RepoFactory.GroupFilter.GetByID(GroupFilterID);
        if (gf == null)
        {
            return;
        }

        gf.CalculateGroupsAndSeries();
        RepoFactory.GroupFilter.Save(gf);
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_RefreshGroupFilter_{GroupFilterID}";
    }

    public override bool LoadFromDBCommand(CommandRequest cq)
    {
        CommandID = cq.CommandID;
        CommandRequestID = cq.CommandRequestID;
        Priority = cq.Priority;
        CommandDetails = cq.CommandDetails;
        DateTimeUpdated = cq.DateTimeUpdated;
        GroupFilterID = int.Parse(cq.CommandDetails);
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
            CommandDetails = GroupFilterID.ToString(),
            DateTimeUpdated = DateTime.Now
        };
        return cq;
    }

    public CommandRequest_RefreshGroupFilter(ILoggerFactory loggerFactory) : base(loggerFactory)
    {
    }

    protected CommandRequest_RefreshGroupFilter()
    {
    }
}
