using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Import;

namespace Shoko.Server.CommandQueue.Commands.Server
{
    public class CmdServerGetImages : BaseCommand, ICommand
    {


        public string ParallelTag { get; set; } = WorkTypes.Server;
        public int ParallelMax { get; set; } = 8;
        public int Priority { get; set; } = 1;

        public string Id => $"GetImages";

        public QueueStateStruct PrettyDescription => new QueueStateStruct { QueueState = QueueStateEnum.GetImages, ExtraParams = new string[0] };
        public string WorkType => WorkTypes.Server;

        public CmdServerGetImages()
        {
        }



        public CmdServerGetImages(string str)
        {
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info($"Getting Images...");
            try
            {
                ReportInit(progress);
                Importer.RunImport_GetImages();
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CmdServerGetImages - {ex}", ex);
            }
        }
    }
}
