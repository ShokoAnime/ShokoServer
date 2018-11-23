using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.Server
{
    public class CmdServerRefreshGroupFilter : BaseCommand, ICommand
    {
        public int GroupFilterID { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.Server;
        public int ParallelMax { get; set; } = 8;
        public int Priority { get; set; } = 9;

        public string Id => $"ServerRefreshGroupFilter_{GroupFilterID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.RefreshGroupFilter,
            ExtraParams = new[] {GroupFilterID.ToString()}
        };

        public string WorkType => WorkTypes.Server;


        public CmdServerRefreshGroupFilter(int groupFilterID)
        {
            GroupFilterID = groupFilterID;
        }

        public CmdServerRefreshGroupFilter(string str) : base(str)
        {
        }


        public override void Run(IProgress<ICommand> progress = null)
        {
            try
            {
                ReportInit(progress);
                ReportUpdate(progress,10);
                using (var upd = Repo.Instance.GroupFilter.BeginAddOrUpdate(GroupFilterID))
                {
                    if (upd.IsNew())
                    {
                        ReportFinish(progress);
                        return;
                    }
                    upd.Entity.CalculateGroupsAndSeries();
                    upd.Commit();
                }
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing ServerRefreshGroupFilter: {GroupFilterID} - {ex}", ex);
            }
        }      
    }
}