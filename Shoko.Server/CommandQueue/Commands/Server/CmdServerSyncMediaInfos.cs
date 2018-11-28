using System;
using System.Linq;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.WebCache;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.Server
{
    public class CmdServerSyncMediaInfos : BaseCommand, ICommand
    {


        public string ParallelTag { get; set; } = WorkTypes.Server;
        public int ParallelMax { get; set; } = 8;
        public int Priority { get; set; } = 1;

        public string Id => $"SyncMediaInfos";

        public QueueStateStruct PrettyDescription => new QueueStateStruct { QueueState = QueueStateEnum.SyncMediaInfos, ExtraParams = new string[0] };
        public string WorkType => WorkTypes.Server;

        public CmdServerSyncMediaInfos()
        {
        }



        public CmdServerSyncMediaInfos(string str)
        {
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info($"Sync all media infos.");
            try
            {
                ReportInit(progress);
                WebCacheAPI.Instance.AddMediaInfo(Repo.Instance.VideoLocal.GetAll().Select(a => a.ToMediaRequest()));
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CmdServerSyncAllMediaInfos - {ex}", ex);
            }
        }
    }
}

