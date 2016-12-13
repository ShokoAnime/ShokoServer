using System;
using JMMServer.Entities;
using JMMServer.Repositories;

namespace JMMServer.Commands
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
                return new QueueStateStruct() { queueState = QueueStateEnum.RefreshGroupFilter, extraParams = new string[] { GroupFilterID.ToString() } };
            }
        }

        public override void ProcessCommand()
        {
	        GroupFilter gf = RepoFactory.GroupFilter.GetByID(GroupFilterID);
	        if (gf == null) return;
	        gf.EvaluateAnimeSeries();
	        gf.EvaluateAnimeGroups();
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
            CommandRequest cq = new CommandRequest();
            cq.CommandID = this.CommandID;
            cq.CommandType = this.CommandType;
            cq.Priority = this.Priority;
            cq.CommandDetails = GroupFilterID.ToString();
            cq.DateTimeUpdated = DateTime.Now;
            return cq;
        }
    }
}