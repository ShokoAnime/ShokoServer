using System;
using System.Collections.Generic;
using System.Text;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Import;

namespace Shoko.Server.CommandQueue.Commands.Schedule
{
    public class CmdScheduleShortUpdate : BaseCommand, ICommand
    {

        public const int UpdateTimeInSeconds = 30;

        public string ParallelTag { get; set; } = WorkTypes.Server;
        public int ParallelMax { get; set; } = 8;
        public int Priority { get; set; } = 1;

        public string Id => $"ShortUpdate";

        public QueueStateStruct PrettyDescription => new QueueStateStruct { QueueState = QueueStateEnum.ShortUpdate, ExtraParams = new string[0] };
        public string WorkType => WorkTypes.Server;

        public CmdScheduleShortUpdate()
        {
        }



        public CmdScheduleShortUpdate(string str)
        {
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info($"Getting Updates...");
            try
            {
                ReportInit(progress);                
                //Do Nothing ???
                Queue.Instance.Add(new CmdScheduleShortUpdate(), TriggerDateTime.AddSeconds(UpdateTimeInSeconds));
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CmdScheduleShortUpdate - {ex}", ex);
            }
        }
    }
}

