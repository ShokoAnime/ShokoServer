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

        public string ParallelTag { get; set; } = WorkTypes.Server.ToString();
        public int ParallelMax { get; set; } = 8;
        public int Priority { get; set; } = 9;

        public string Id => $"ServerRefreshGroupFilter_{GroupFilterID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.RefreshGroupFilter,
            ExtraParams = new[] {GroupFilterID.ToString()}
        };

        public WorkTypes WorkType => WorkTypes.Server;


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
                InitProgress(progress);
                SVR_GroupFilter gf = Repo.Instance.GroupFilter.GetByID(GroupFilterID);
                if (gf == null) 
                {
                    ReportFinishAndGetResult(progress);
                    return;
                }
                UpdateAndReportProgress(progress,10);
                using (var upd = Repo.Instance.GroupFilter.BeginAddOrUpdate(() => gf))
                {
                    upd.Entity.CalculateGroupsAndSeries();
                    upd.Commit();
                }
                ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                ReportErrorAndGetResult(progress, $"Error processing ServerRefreshGroupFilter: {GroupFilterID} - {ex}", ex);
            }
        }      
    }
}