using System;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Server;

namespace Shoko.Server.Commands.AniDB;

[Serializable]
[Command(CommandRequestType.AniDB_GetTitles)]
[Obsolete("Use the xml from the site")]
public class CommandRequest_GetAniDBTitles : CommandRequestImplementation
{
    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Getting AniDB titles",
        queueState = QueueStateEnum.AniDB_GetTitles,
        extraParams = Array.Empty<string>()
    };

    protected override void Process()
    {
        Logger.LogInformation("CommandRequest_GetAniDBTitles is deprecated. Skipping");
    }

    /// <summary>
    /// This should generate a unique key for a command
    /// It will be used to check whether the command has already been queued before adding it
    /// </summary>
    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_GetAniDBTitles_{DateTime.Now.ToString(CultureInfo.InvariantCulture)}";
    }

    protected override bool Load()
    {
        return true;
    }

    public CommandRequest_GetAniDBTitles(ILoggerFactory loggerFactory) : base(loggerFactory)
    {
    }

    protected CommandRequest_GetAniDBTitles()
    {
    }
}
