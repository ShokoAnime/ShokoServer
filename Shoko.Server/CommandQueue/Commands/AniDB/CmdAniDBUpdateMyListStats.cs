using System;
using System.Collections.Generic;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.CommandQueue.Preconditions;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.CommandQueue.Commands.AniDB
{

    public class CmdAniDBUpdateMyListStats : BaseCommand, ICommand
    {
        public bool ForceRefresh { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.AniDB;
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 7;
        public string Id => "UpdateMyListStats";
        public string WorkType => WorkTypes.AniDB;
        public override List<Type> GenericPreconditions => new List<Type> { typeof(AniDBUDPBan) };
        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            QueueState = QueueStateEnum.UpdateMyListStats,
            ExtraParams = new [] { ForceRefresh.ToString()}
        };

     
        public CmdAniDBUpdateMyListStats(string str) : base(str)
        {
        }

        public CmdAniDBUpdateMyListStats(bool forced)
        {
            ForceRefresh = forced;
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_UpdateMyListStats");

            try
            {
                ReportInit(progress);
                // we will always assume that an anime was downloaded via http first

                ScheduledUpdate sched = Repo.Instance.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBMylistStats);
                ReportUpdate(progress,30);
                if (sched != null)
                {
                    int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDb.MyListStats_UpdateFrequency);

                    // if we have run this in the last 24 hours and are not forcing it, then exit
                    TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!ForceRefresh) ReportFinish(progress);
                    }
                }

                using (var upd = Repo.Instance.ScheduledUpdate.BeginAddOrUpdate(
                    () => sched,
                    () => new ScheduledUpdate { UpdateType = (int)ScheduledUpdateType.AniDBMylistStats, UpdateDetails = string.Empty }
                    ))
                {
                    upd.Entity.LastUpdate = DateTime.Now;
                    upd.Commit();
                }
                ReportUpdate(progress,60);

                ShokoService.AnidbProcessor.UpdateMyListStats();
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing Command AniDB.UpdateMyListStats: {ex}", ex);
            }
        }
    }
}