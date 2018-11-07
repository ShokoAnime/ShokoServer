using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.WebCache;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.WebCache
{
    public class CmdWebCacheSendXRefAniDBOther : BaseCommand, ICommand
    {
        public int CrossRef_AniDB_OtherID { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.WebCache.ToString();
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public WorkTypes WorkType => WorkTypes.WebCache;

        public string Id => $"WebCacheSendXRefAniDBOther_{CrossRef_AniDB_OtherID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.WebCacheSendXRefAniDBOther,
            ExtraParams = new[] {CrossRef_AniDB_OtherID.ToString()}
        };


        public CmdWebCacheSendXRefAniDBOther(string str) : base(str)
        {
        }

        public CmdWebCacheSendXRefAniDBOther(int xrefID)
        {
            CrossRef_AniDB_OtherID = xrefID;
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            try
            {
                InitProgress(progress);
                CrossRef_AniDB_Other xref = Repo.Instance.CrossRef_AniDB_Other.GetByID(CrossRef_AniDB_OtherID);
                if (xref == null)
                {
                    ReportFinishAndGetResult(progress);
                    return;
                }
                UpdateAndReportProgress(progress,50);
                WebCacheAPI.Send_CrossRefAniDBOther(xref);
                ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                ReportErrorAndGetResult(progress, $"Error processing WebCacheSendXRefAniDBOther {CrossRef_AniDB_OtherID} - {ex}", ex);
            }
        }


    }
}