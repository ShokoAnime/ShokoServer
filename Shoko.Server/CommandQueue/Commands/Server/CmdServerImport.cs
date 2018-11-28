using System;
using System.Collections.Generic;
using System.Text;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Import;
using Shoko.Server.Models;

namespace Shoko.Server.CommandQueue.Commands.Server
{
    public class CmdServerImport : BaseCommand, ICommand
    {


        public string ParallelTag { get; set; } = "IMPORT";
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 1;

        public string Id => $"Import";

        public QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = QueueStateEnum.Import, ExtraParams = new string[0]};
        public string WorkType => WorkTypes.Server;

        public CmdServerImport()
        {
        }



        public CmdServerImport(string str) 
        {
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Starting Import.");
            try
            {
                ReportInit(progress);
                Importer.RunImport_NewFiles();
                ReportUpdate(progress,13);
                Importer.RunImport_IntegrityCheck();
                ReportUpdate(progress, 25);

                // drop folder
                Importer.RunImport_DropFolders();
                ReportUpdate(progress, 38);
                // TvDB association checks
                Importer.RunImport_ScanTvDB();
                ReportUpdate(progress, 50);

                // Trakt association checks
                Importer.RunImport_ScanTrakt();
                ReportUpdate(progress, 63);

                // MovieDB association checks
                Importer.RunImport_ScanMovieDB();
                ReportUpdate(progress, 75);

                // Check for missing images
                Importer.RunImport_GetImages();
                ReportUpdate(progress, 88);
            
                // Check for previously ignored files
                Importer.CheckForPreviouslyIgnored();
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CmdServerImport - {ex}", ex);
            }
        }
    }
}
