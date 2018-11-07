using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.Server
{

    public class CmdServerReadMediaInfo : BaseCommand, ICommand
    {
        public int VideoLocalID { get; set; }


        public string ParallelTag { get; set; } = WorkTypes.Server.ToString();
        public int ParallelMax { get; set; } = 8;
        public int Priority { get; set; } = 4;

        public string Id => $"ReadMediaInfo_{VideoLocalID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.ReadingMedia,
            ExtraParams = new[] {VideoLocalID.ToString()}
        };

        public WorkTypes WorkType => WorkTypes.Server;

        public CmdServerReadMediaInfo(string str) : base(str)
        {
        }

        public CmdServerReadMediaInfo(int vidID)
        {
            VideoLocalID = vidID;
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Reading Media Info for File: {0}", VideoLocalID);


            try
            {
                InitProgress(progress);
                SVR_VideoLocal vlocal = Repo.Instance.VideoLocal.GetByID(VideoLocalID);
                SVR_VideoLocal_Place place = vlocal?.GetBestVideoLocalPlace(true);
                UpdateAndReportProgress(progress,50);
                if (place == null)
                {
                    ReportErrorAndGetResult(progress, $"Could not find VideoLocal: {VideoLocalID}");
                    return;
                }
                using (var txn = Repo.Instance.VideoLocal.BeginAddOrUpdate(() => place.VideoLocal))
                {
                    if (place.RefreshMediaInfo(txn.Entity))
                    {
                        txn.Commit();
                    }
                }

                ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                ReportErrorAndGetResult(progress, $"Error processing ServerReadMediaInfo: {VideoLocalID} - {ex}", ex);
            }
        }
    }
}