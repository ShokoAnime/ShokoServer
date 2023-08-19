using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Server;

namespace Shoko.Server.Commands;

[Command(CommandRequestType.NullCommand)]
public class CommandRequest_Null : CommandRequestImplementation
{
    protected override void Process()
    {
    }

    protected override bool Load()
    {
        return true;
    }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;
    public override QueueStateStruct PrettyDescription => new();

    protected override string GetCommandDetails()
    {
        return string.Empty;
    }

    public override void GenerateCommandID()
    {
        CommandID = nameof(CommandRequest_Null);
    }

    public CommandRequest_Null(ILoggerFactory loggerFactory) : base(loggerFactory)
    {
    }

    protected CommandRequest_Null()
    {
    }
}
