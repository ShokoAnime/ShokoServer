using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

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

        protected override void Process(IServiceProvider serviceProvider)
        {
            if (GroupFilterID == 0)
            {
                RepoFactory.GroupFilter.CreateOrVerifyLockedFilters();
                return;
            }
            SVR_GroupFilter gf = RepoFactory.GroupFilter.GetByID(GroupFilterID);
            if (gf == null) return;
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