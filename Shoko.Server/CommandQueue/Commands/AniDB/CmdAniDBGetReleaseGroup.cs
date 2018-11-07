using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.AniDB
{

    public class CmdAniDBGetReleaseGroup : BaseCommand, ICommand
    {
        public int GroupID { get; set; }
        public bool ForceRefresh { get; set; }


        public string ParallelTag { get; set; } = WorkTypes.AniDB.ToString();
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 5;
        public string Id => $"GetReleaseGroup_{GroupID}";
        public WorkTypes WorkType => WorkTypes.AniDB;

        public QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = QueueStateEnum.GetReleaseInfo, ExtraParams = new[] {GroupID.ToString(), ForceRefresh.ToString()}};



        public CmdAniDBGetReleaseGroup(string str) : base(str)
        {
        }

        public CmdAniDBGetReleaseGroup(int grpid, bool forced)
        {
            GroupID = grpid;
            ForceRefresh = forced;

        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_GetReleaseGroup: {0}", GroupID);

            try
            {
                InitProgress(progress);
                AniDB_ReleaseGroup relGroup = Repo.Instance.AniDB_ReleaseGroup.GetByGroupID(GroupID);
                UpdateAndReportProgress(progress,50);
                if (ForceRefresh || relGroup == null)
                {
                    // redownload anime details from http ap so we can get an update character list
                    ShokoService.AnidbProcessor.GetReleaseGroupUDP(GroupID);
                }

                ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                ReportErrorAndGetResult(progress, $"Error processing Command AniDb.GetReleaseGroup: {GroupID} - {ex}", ex);
            }
        }
    }
}