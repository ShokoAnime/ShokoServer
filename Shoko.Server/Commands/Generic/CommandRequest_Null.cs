using System;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Server;

namespace Shoko.Server.Commands
{
    [Command(CommandRequestType.NullCommand)]
    class CommandRequest_Null : CommandRequestImplementation
    {
        protected override void Process()
        {
            
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            return true;
        }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;
        public override QueueStateStruct PrettyDescription => new QueueStateStruct();
        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();
            return new CommandRequest
            {
                CommandID = CommandID,
                CommandType = CommandType,
                Priority = Priority,
                DateTimeUpdated = DateTime.Now,
                CommandDetails = ""
            };
        }

        public override void GenerateCommandID()
        {
            CommandID = nameof(CommandRequest_Null);
        }

        public CommandRequest_Null(ILoggerFactory loggerFactory) : base(loggerFactory)
        {
        }
    }
}
