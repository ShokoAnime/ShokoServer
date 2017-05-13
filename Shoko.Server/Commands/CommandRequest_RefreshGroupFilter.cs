using System;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandRequest_RefreshGroupFilter : CommandRequestImplementation, ICommandRequest
    {
        public int GroupFilterID { get; set; }

        public CommandRequest_RefreshGroupFilter(int groupFilterID)
        {
            GroupFilterID = groupFilterID;

            this.CommandType = (int) CommandRequestType.Refresh_GroupFilter;
            this.Priority = (int) DefaultPriority;
            GenerateCommandID();
        }

        public CommandRequest_RefreshGroupFilter()
        {
        }


        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority6; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct()
                {
                    queueState = QueueStateEnum.RefreshGroupFilter,
                    extraParams = new string[] {GroupFilterID.ToString()}
                };
            }
        }

        public override void ProcessCommand()
        {
            SVR_GroupFilter gf = RepoFactory.GroupFilter.GetByID(GroupFilterID);
            if (gf == null) return;
            gf.CalculateGroupsAndSeries();
            RepoFactory.GroupFilter.Save(gf);
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_RefreshGroupFilter_{0}", this.GroupFilterID);
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            this.CommandID = cq.CommandID;
            this.CommandRequestID = cq.CommandRequestID;
            this.CommandType = cq.CommandType;
            this.Priority = cq.Priority;
            this.CommandDetails = cq.CommandDetails;
            this.DateTimeUpdated = cq.DateTimeUpdated;
            GroupFilterID = int.Parse(cq.CommandDetails);
            return true;
        }


        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();
            CommandRequest cq = new CommandRequest
            {
                CommandID = this.CommandID,
                CommandType = this.CommandType,
                Priority = this.Priority,
                CommandDetails = GroupFilterID.ToString(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}