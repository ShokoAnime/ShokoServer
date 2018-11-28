using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Import;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.Server
{
    public class CmdServerReadAllMediaInfo : BaseCommand, ICommand
    {


        public string ParallelTag { get; set; } = "IMPORT";
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 1;

        public string Id => $"ScanDropFolders";

        public QueueStateStruct PrettyDescription => new QueueStateStruct { QueueState = QueueStateEnum.RefreshAllMediaInfo, ExtraParams = new string[0] };
        public string WorkType => WorkTypes.Server;

        public CmdServerReadAllMediaInfo()
        {
        }



        public CmdServerReadAllMediaInfo(string str)
        {
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info($"Refreshing all media infos.");
            try
            {
                ReportInit(progress);
                Queue.Instance.AddRange(Repo.Instance.VideoLocal.GetAll().Select(a => new CmdServerReadMediaInfo(a.VideoLocalID)));
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CmdServerScanDropFolders - {ex}", ex);
            }
        }
    }
}

