using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.Trakt
{
     public class CmdTraktUpdateAllSeries : BaseCommand<CmdTraktUpdateAllSeries>, ICommand
    {
        public bool ForceRefresh { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.Trakt.ToString();
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 6;
        public string Id => "TraktUpdateAllSeries";
        public QueueStateStruct PrettyDescription => new QueueStateStruct {queueState = QueueStateEnum.UpdateTrakt, extraParams = new [] { ForceRefresh.ToString()}};
        public WorkTypes WorkType => WorkTypes.Trakt;


        public CmdTraktUpdateAllSeries(string str) : base(str)
        {
        }

        public CmdTraktUpdateAllSeries(bool forced)
        {
            ForceRefresh = forced;
        }

        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            logger.Info("Processing CommandRequest_TraktUpdateAllSeries");

            try
            {
                InitProgress(progress);
                using (var txn = Repo.Instance.ScheduledUpdate.BeginAddOrUpdate(
                    () => Repo.Instance.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TraktUpdate),
                    () => new ScheduledUpdate { UpdateType = (int)ScheduledUpdateType.TraktUpdate, UpdateDetails = string.Empty }
                    ))
                {
                    
                    if (!txn.IsUpdate)
                    {
                        int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.TraktTv.UpdateFrequency);

                        // if we have run this in the last xxx hours then exit
                        TimeSpan tsLastRun = DateTime.Now - txn.Entity.LastUpdate;
                        if (tsLastRun.TotalHours < freqHours && !ForceRefresh)
                            return ReportFinishAndGetResult(progress);
                    }
                    txn.Entity.LastUpdate = DateTime.Now;
                    txn.Commit();
                }
                UpdateAndReportProgress(progress,30);
                // update all info
                TraktTVHelper.UpdateAllInfo();

                UpdateAndReportProgress(progress, 60);
                // scan for new matches
                TraktTVHelper.ScanForMatches();
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing CommandRequest_TraktUpdateAllSeries: {ex}", ex);
            }
        }

    }
}