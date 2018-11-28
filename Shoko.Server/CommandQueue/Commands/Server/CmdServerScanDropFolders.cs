using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Import;

namespace Shoko.Server.CommandQueue.Commands.Server
{
    public class CmdServerScanDropFolders : BaseCommand, ICommand
    {


        public string ParallelTag { get; set; } = "IMPORT";
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 1;

        public string Id => $"ScanDropFolders";

        public QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = QueueStateEnum.ScanDropFolders, ExtraParams = new string[0]};
        public string WorkType => WorkTypes.Server;

        public CmdServerScanDropFolders()
        {
        }



        public CmdServerScanDropFolders(string str)
        {
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info($"Starting Scan Drop Folders.");
            try
            {
                ReportInit(progress);
                Importer.RunImport_DropFolders();
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CmdServerScanDropFolders - {ex}", ex);
            }
        }
    }
}
