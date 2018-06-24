using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Commons.Queue;
using Shoko.Models.Server;

namespace Shoko.Server.Commands
{
    [Command(CommandRequestType.NullCommand)]
    class CommandRequest_Null : CommandRequestImplementation
    {
        public CommandRequest_Null()
        {
            DefaultPriority = CommandRequestPriority.Priority5;

        }

        public override void ProcessCommand()
        {
            
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            return true;
        }

        public override CommandRequestPriority DefaultPriority { get; }
        public override QueueStateStruct PrettyDescription => new QueueStateStruct();
        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();
            return new CommandRequest()
            {
                CommandID = CommandID,
                CommandType = this.CommandType,
                Priority = Priority,
                DateTimeUpdated = DateTime.Now,
                CommandDetails = ""
            };
        }

        public override void GenerateCommandID()
        {
            CommandID = nameof(CommandRequest_Null);
        }
    }
}
