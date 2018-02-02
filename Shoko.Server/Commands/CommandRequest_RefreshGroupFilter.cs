using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandRequest_RefreshGroupFilter : CommandRequest
    {
        public virtual int GroupFilterID { get; set; }

        public override string CommandDetails => GroupFilterID.ToString();

        public CommandRequest_RefreshGroupFilter(int groupFilterID)
        {
            GroupFilterID = groupFilterID;

            CommandType = (int) CommandRequestType.Refresh_GroupFilter;
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
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_RefreshGroupFilter_{GroupFilterID}";
        }

        public override bool InitFromDB(Shoko.Models.Server.CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            CommandType = cq.CommandType;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;
            GroupFilterID = int.Parse(cq.CommandDetails);
            return true;
        }
    }
}