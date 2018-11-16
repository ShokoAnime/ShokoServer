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

    public class CmdTraktSyncCollection : BaseCommand, ICommand
    {
        public bool ForceRefresh { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.Trakt;
        public int ParallelMax { get; set; } = 4;
        public int Priority { get; set; } = 8;
        public string Id => "TraktSyncCollection";

        public QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = QueueStateEnum.SyncTrakt, ExtraParams = new [] { ForceRefresh.ToString()}};
        public string WorkType => WorkTypes.Trakt;


        public CmdTraktSyncCollection(string str) : base(str)
        {
        }

        public CmdTraktSyncCollection(bool forced)
        {
            ForceRefresh = forced;
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_TraktSyncCollection");

            try
            {
                ReportInit(progress);
                if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                {
                    ReportFinish(progress);
                    return;
                }

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
                        {
                            ReportFinish(progress);
                            return;
                        }
                    }
                    upd.Entity.LastUpdate = DateTime.Now;
                    upd.Commit();
                }
                ReportUpdate(progress,50);
                TraktTVHelper.SyncCollectionToTrakt();
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CommandRequest_TraktSyncCollection: {ex}", ex);
            }
        }
    }
}