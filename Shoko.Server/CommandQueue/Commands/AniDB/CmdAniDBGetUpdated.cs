using System;
using System.Collections.Generic;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.AniDB
{
    public class CmdAniDBGetUpdated : BaseCommand<CmdAniDBGetUpdated>, ICommand
    {
        public CmdAniDBGetUpdated(string str) : base(str)
        {
        }

        public CmdAniDBGetUpdated(bool forced)
        {
            ForceRefresh = forced;
        }

        public bool ForceRefresh { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.AniDB.ToString();
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 4;
        public string Id => "GetUpdated";
        public WorkTypes WorkType => WorkTypes.AniDB;

        public QueueStateStruct PrettyDescription => new QueueStateStruct {queueState = QueueStateEnum.GetUpdatedAnime, extraParams = new [] { ForceRefresh.ToString()}};

        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            logger.Info("Processing CommandRequest_GetUpdated");

            try
            {
                InitProgress(progress);
                List<int> animeIDsToUpdate = new List<int>();

                // check the automated update table to see when the last time we ran this command
                ScheduledUpdate sched = Repo.Instance.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBUpdates);
                UpdateAndReportProgress(progress,20);
                if (sched != null)
                {
                    int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDb.Anime_UpdateFrequency);

                    // if we have run this in the last 12 hours and are not forcing it, then exit
                    TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!ForceRefresh) return ReportFinishAndGetResult(progress);
                    }
                }


                long webUpdateTime;
                long webUpdateTimeNew;
                if (sched == null)
                {
                    // if this is the first time, lets ask for last 3 days
                    DateTime localTime = DateTime.Now.AddDays(-3);
                    DateTime utcTime = localTime.ToUniversalTime();
                    webUpdateTime = long.Parse(Commons.Utils.AniDB.AniDBDate(utcTime));
                    webUpdateTimeNew = long.Parse(Commons.Utils.AniDB.AniDBDate(DateTime.Now.ToUniversalTime()));

                    //sched = new ScheduledUpdate { UpdateType = (int)ScheduledUpdateType.AniDBUpdates };
                }
                else
                {
                    logger.Trace("Last anidb info update was : {0}", sched.UpdateDetails);
                    webUpdateTime = long.Parse(sched.UpdateDetails);
                    webUpdateTimeNew = long.Parse(Commons.Utils.AniDB.AniDBDate(DateTime.Now.ToUniversalTime()));

                    logger.Info($"{Utils.FormatSecondsToDisplayTime(int.Parse((webUpdateTimeNew - webUpdateTime).ToString()))} since last UPDATED command");
                }

                // get a list of updates from AniDB
                // startTime will contain the date/time from which the updates apply to
                ShokoService.AnidbProcessor.GetUpdated(ref animeIDsToUpdate, ref webUpdateTime);
                UpdateAndReportProgress(progress,40);
                // now save the update time from AniDB
                // we will use this next time as a starting point when querying the web cache
                using (var upd = Repo.Instance.ScheduledUpdate.BeginAddOrUpdate(() => sched, () => new ScheduledUpdate {UpdateType = (int) ScheduledUpdateType.AniDBUpdates}))
                {
                    upd.Entity.LastUpdate = DateTime.Now;
                    upd.Entity.UpdateDetails = webUpdateTimeNew.ToString();
                    upd.Commit();
                }

                if (animeIDsToUpdate.Count == 0)
                {
                    logger.Info("No anime to be updated");
                    return ReportFinishAndGetResult(progress);
                }

                UpdateAndReportProgress(progress,60);


                int countAnime = 0;
                int countSeries = 0;
                List<ICommand> updates = new List<ICommand>();
                foreach (int animeID in animeIDsToUpdate)
                {
                    // update the anime from HTTP
                    SVR_AniDB_Anime anime = Repo.Instance.AniDB_Anime.GetByAnimeID(animeID);
                    if (anime == null)
                    {
                        logger.Trace("No local record found for Anime ID: {0}, so skipping...", animeID);
                        continue;
                    }

                    logger.Info("Updating CommandRequest_GetUpdated: {0} ", animeID);

                    var update = Repo.Instance.AniDB_AnimeUpdate.GetByAnimeID(animeID);

                    // but only if it hasn't been recently updated
                    TimeSpan ts = DateTime.Now - update.UpdatedAt;
                    if (ts.TotalHours > 4)
                    {
                        updates.Add(new CmdAniDBGetAnimeHTTP(animeID, true, false));
                        countAnime++;
                    }

                    // update the group status
                    // this will allow us to determine which anime has missing episodes
                    // so we wonly get by an amime where we also have an associated series
                    SVR_AnimeSeries ser = Repo.Instance.AnimeSeries.GetByAnimeID(animeID);
                    if (ser != null)
                    {
                        updates.Add(new CmdAniDBGetReleaseGroupStatus(animeID, true));
                        countSeries++;
                    }
                }

                if (updates.Count > 0)
                    Queue.Instance.AddRange(updates);

                logger.Info("Updating {0} anime records, and {1} group status records", countAnime, countSeries);
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing AniDb.GetUpdated: {ex}", ex);
            }
        }
    }
}