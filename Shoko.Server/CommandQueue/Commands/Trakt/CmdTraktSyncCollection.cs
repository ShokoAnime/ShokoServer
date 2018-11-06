using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.Trakt
{

    public class CmdTraktSyncCollection : BaseCommand<CmdTraktSyncCollection>, ICommand
    {
        public bool ForceRefresh { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.Trakt.ToString();
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 8;
        public string Id => "TraktSyncCollection";

        public QueueStateStruct PrettyDescription => new QueueStateStruct {queueState = QueueStateEnum.SyncTrakt, extraParams = new [] { ForceRefresh.ToString()}};
        public WorkTypes WorkType => WorkTypes.Trakt;


        public CmdTraktSyncCollection(string str) : base(str)
        {
        }

        public CmdTraktSyncCollection(bool forced)
        {
            ForceRefresh = forced;
        }

        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            logger.Info("Processing CommandRequest_TraktSyncCollection");

            try
            {
                InitProgress(progress);
                if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken)) return ReportFinishAndGetResult(progress);

                using (var upd = Repo.Instance.ScheduledUpdate.BeginAddOrUpdate(
                    () => Repo.Instance.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.TraktSync),
                    () => new ScheduledUpdate { UpdateType = (int)ScheduledUpdateType.TraktSync, UpdateDetails = string.Empty }
                    ))
                {
                    if (upd.IsUpdate)
                    {
                        int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.TraktTv.SyncFrequency);

                        // if we have run this in the last xxx hours then exit
                        TimeSpan tsLastRun = DateTime.Now - upd.Entity.LastUpdate;
                        if (tsLastRun.TotalHours < freqHours && !ForceRefresh)
                            return ReportFinishAndGetResult(progress);
                    }
                    upd.Entity.LastUpdate = DateTime.Now;
                    upd.Commit();
                }
                UpdateAndReportProgress(progress,50);
                TraktTVHelper.SyncCollectionToTrakt();
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing CommandRequest_TraktSyncCollection: {ex}", ex);
            }
        }
    }
}