using System;
using System.Collections.Generic;
using System.Text;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Import;

namespace Shoko.Server.CommandQueue.Commands.Schedule
{
    public class CmdScheduleUpdate : BaseCommand, ICommand
    {

        public const int UpdateTimeInSeconds = 60 * 5;

        public string ParallelTag { get; set; } = WorkTypes.Server;
        public int ParallelMax { get; set; } = 8;
        public int Priority { get; set; } = 1;

        public string Id => $"Update";

        public QueueStateStruct PrettyDescription => new QueueStateStruct { QueueState = QueueStateEnum.Update, ExtraParams = new string[0] };
        public string WorkType => WorkTypes.Server;

        public CmdScheduleUpdate()
        {
        }



        public CmdScheduleUpdate(string str)
        {
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info($"Getting Updates...");
            try
            {
                ReportInit(progress);
                Importer.CheckForDayFilters();
                ReportUpdate(progress,9);
                Importer.CheckForCalendarUpdate(false);
                ReportUpdate(progress, 18);
                Importer.CheckForAnimeUpdate(false);
                ReportUpdate(progress, 27);
                Importer.CheckForTvDBUpdates(false);
                ReportUpdate(progress, 36);
                Importer.CheckForMyListSyncUpdate(false);
                ReportUpdate(progress, 45);
                Importer.CheckForTraktAllSeriesUpdate(false);
                ReportUpdate(progress, 55);
                Importer.CheckForTraktTokenUpdate(false);
                ReportUpdate(progress, 64);
                Importer.CheckForMyListStatsUpdate(false);
                ReportUpdate(progress, 73);
                Importer.CheckForAniDBFileUpdate(false);
                ReportUpdate(progress, 82);
                Importer.UpdateAniDBTitles();
                ReportUpdate(progress, 91);
                Importer.SendUserInfoUpdate(false);
                Queue.Instance.Add(new CmdScheduleUpdate(), TriggerDateTime.AddSeconds(UpdateTimeInSeconds));
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CmdScheduleUpdate - {ex}", ex);
            }
        }
    }
}

