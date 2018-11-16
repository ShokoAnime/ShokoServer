using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.CommandQueue.Commands.Trakt
{
     public class CmdTraktUpdateAllSeries : BaseCommand, ICommand
    {
        public bool ForceRefresh { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.Trakt;
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 6;
        public string Id => "TraktUpdateAllSeries";
        public QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = QueueStateEnum.UpdateTrakt, ExtraParams = new [] { ForceRefresh.ToString()}};
        public string WorkType => WorkTypes.Trakt;


        public CmdTraktUpdateAllSeries(string str) : base(str)
        {
        }

        public CmdTraktUpdateAllSeries(bool forced)
        {
            ForceRefresh = forced;
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_TraktUpdateAllSeries");

            try
            {
                ReportInit(progress);
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
                        {
                            ReportFinish(progress);
                            return;
                        }
                    }
                    txn.Entity.LastUpdate = DateTime.Now;
                    txn.Commit();
                }
                ReportUpdate(progress,30);
                // update all info
                TraktTVHelper.UpdateAllInfo();

                ReportUpdate(progress, 60);
                // scan for new matches
                TraktTVHelper.ScanForMatches();
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CommandRequest_TraktUpdateAllSeries: {ex}", ex);
            }
        }

    }
}