using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;

namespace Shoko.Server.CommandQueue.Commands.Schedule
{
    public class CmdScheduleLogRotation : BaseCommand, ICommand
    {

        public const int LogRotationTimeInSeconds = 60 * 60 * 24; //Moving to settings

        public string ParallelTag { get; set; } = WorkTypes.Schedule;
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 1;

        public string Id => $"LogRotation";


        public QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = QueueStateEnum.LogRotation, ExtraParams = new string[0]};

        public string WorkType => WorkTypes.Schedule;

        public CmdScheduleLogRotation(string str) 
        {
        }

        public CmdScheduleLogRotation()
        {
        }


        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Executing log rotation.");
            try
            {
                ReportInit(progress);
                LogRotator.Instance.Start();
                Queue.Instance.Add(new CmdScheduleLogRotation(), TriggerDateTime.AddSeconds(LogRotationTimeInSeconds));
                ReportFinish(progress);
            }
            catch (Exception e)
            {
                ReportError(progress, $"Error processing CmdLogRotation:", e);
            }

        }
    }
}
