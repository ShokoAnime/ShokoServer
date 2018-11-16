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


        public string ParallelTag { get; set; } = WorkTypes.Server;
        public int ParallelMax { get; set; } = 8;
        public int Priority { get; set; } = 4;

        public string Id => $"ReadMediaInfo_{VideoLocalID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.ReadingMedia,
            ExtraParams = new[] {VideoLocalID.ToString()}
        };

        public string WorkType => WorkTypes.Server;

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
                ReportInit(progress);
                SVR_VideoLocal vlocal = Repo.Instance.VideoLocal.GetByID(VideoLocalID);
                SVR_VideoLocal_Place place = vlocal?.GetBestVideoLocalPlace(true);
                ReportUpdate(progress,50);
                if (place == null)
                {
                    ReportError(progress, $"Could not find VideoLocal: {VideoLocalID}");
                    return;
                }
                using (var txn = Repo.Instance.VideoLocal.BeginAddOrUpdate(() => place.VideoLocal))
                {
                    if (place.RefreshMediaInfo(txn.Entity))
                    {
                        txn.Commit();
                    }
                }

                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing ServerReadMediaInfo: {VideoLocalID} - {ex}", ex);
            }
        }
    }
}