using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.WebCache;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.WebCache
{
    public class CmdWebCacheSendAniDBXRef : BaseCommand, ICommand
    {
        public int CrossRef_AniDB_ProviderID { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.WebCache;
        public int ParallelMax { get; set; } = 2;
        public int Priority { get; set; } = 10;
        public string WorkType => WorkTypes.WebCache;

        public string Id => $"WebCacheSendXRefAniDB_{CrossRef_AniDB_ProviderID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.WebCacheSendXRefAniDBOther,
            ExtraParams = new[] { CrossRef_AniDB_ProviderID.ToString()}
        };


        public CmdWebCacheSendAniDBXRef(string str) : base(str)
        {
        }

        public CmdWebCacheSendAniDBXRef(int xrefID)
        {
            CrossRef_AniDB_ProviderID = xrefID;
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            try
            {
                ReportInit(progress);
                Models.SVR_CrossRef_AniDB_Provider xref = Repo.Instance.CrossRef_AniDB_Provider.GetByID(CrossRef_AniDB_ProviderID);
                if (xref == null)
                {
                    ReportFinish(progress);
                    return;
                }
                ReportUpdate(progress,50);
                WebCacheAPI.Instance.AddCrossRef_AniDB_Provider(xref.ToWebCache(), false);
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing WebCacheSendXRefAniDBOther {CrossRef_AniDB_ProviderID} - {ex}", ex);
            }
        }


    }
}