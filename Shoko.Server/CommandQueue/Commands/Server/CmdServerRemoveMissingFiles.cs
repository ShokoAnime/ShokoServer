using System;
using System.Collections.Generic;
using System.Text;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Import;

namespace Shoko.Server.CommandQueue.Commands.Server
{
    public class CmdServerRemoveMissingFiles : BaseCommand, ICommand
    {

        public string ParallelTag { get; set; } = "IMPORT";
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 1;

        public string Id => $"RemoveMissingFiles";

        public QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = QueueStateEnum.RemoveMissingFiles, ExtraParams = new string[0]};
        public string WorkType => WorkTypes.Server;

        public CmdServerRemoveMissingFiles()
        {
        }



        public CmdServerRemoveMissingFiles(string str)
        {
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info($"Removing Missing Files...");
            try
            {
                ReportInit(progress);
                Importer.RemoveRecordsWithoutPhysicalFiles();
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CmdServerRemoveMissingFiles - {ex}", ex);
            }
        }
    }
}
