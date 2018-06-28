using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Command(CommandRequestType.Refresh_GroupFilter)]
    public class CommandRequest_RefreshGroupFilter : CommandRequestImplementation
    {
        public int GroupFilterID { get; set; }

        public CommandRequest_RefreshGroupFilter(int groupFilterID)
        {
            GroupFilterID = groupFilterID;

            Priority = (int) DefaultPriority;
            GenerateCommandID();
        }

        public CommandRequest_RefreshGroupFilter()
        {
        }


        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority9;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.RefreshGroupFilter,
            extraParams = new[] {GroupFilterID.ToString()}
        };

        public override void ProcessCommand()
        {
            SVR_GroupFilter gf = Repo.GroupFilter.GetByID(GroupFilterID);
            if (gf == null) return;
            SVR_GroupFilter.CalculateGroupsAndSeries(gf);
            Repo.GroupFilter.Save(gf);
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
            CommandRequest cq = new CommandRequest
            {
                CommandID = CommandID,
                CommandType = CommandType,
                Priority = Priority,
                CommandDetails = GroupFilterID.ToString(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}