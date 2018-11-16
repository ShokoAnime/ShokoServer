using System;
using System.Collections.Generic;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.CommandQueue.Commands.AniDB
{
    public class CmdAniDBGetCalendar : BaseCommand, ICommand
    {
        public bool ForceRefresh { get; set; }


        public QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = QueueStateEnum.GetCalendar, ExtraParams = new [] { ForceRefresh.ToString()}};
        public string WorkType => WorkTypes.AniDB;
        public string ParallelTag { get; set; } = WorkTypes.AniDB;
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 5;
        public string Id => "GetCalendar";
        public CmdAniDBGetCalendar(bool forced)
        {
            ForceRefresh = forced;
        }

        public CmdAniDBGetCalendar(string str) : base(str)
        {
        }
        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_GetCalendar");

            try
            {
                // we will always assume that an anime was downloaded via http first
                ReportInit(progress);
                ScheduledUpdate sched = Repo.Instance.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBCalendar);
                ReportUpdate(progress,20);

                if (sched != null)
                {
                    int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDb.Calendar_UpdateFrequency);

                    // if we have run this in the last 12 hours and are not forcing it, then exit
                    TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!ForceRefresh)
                        {
                            ReportFinish(progress);
                            return;
                        }
                    }
                }

                ReportUpdate(progress,40);

                using (var upd = Repo.Instance.ScheduledUpdate.BeginAddOrUpdate(() => sched, () => new ScheduledUpdate {UpdateType = (int) ScheduledUpdateType.AniDBCalendar, UpdateDetails = string.Empty}))
                {
                    upd.Entity.LastUpdate = DateTime.Now;
                    upd.Commit();
                }

                ReportUpdate(progress,60);

                CalendarCollection colCalendars = ShokoService.AnidbProcessor.GetCalendarUDP();
                if (colCalendars == null || colCalendars.Calendars == null)
                {
                    ReportError(progress, "Could not get calendar from AniDB");
                    return;
                }

                ReportUpdate(progress,80);
                List<ICommand> cmds = new List<ICommand>();
                foreach (Calendar cal in colCalendars.Calendars)
                {
                    SVR_AniDB_Anime anime = Repo.Instance.AniDB_Anime.GetByAnimeID(cal.AnimeID);
                    var update = Repo.Instance.AniDB_AnimeUpdate.GetByAnimeID(cal.AnimeID);
                    if (anime != null && update != null)
                    {
                        // don't update if the local data is less 2 days old
                        TimeSpan ts = DateTime.Now - update.UpdatedAt;
                        if (ts.TotalDays >= 2)
                        {
                            cmds.Add(new CmdAniDBGetAnimeHTTP(cal.AnimeID, true, false));
                        }
                        else
                        {
                            // update the release date even if we don't update the anime record
                            if (anime.AirDate != cal.ReleaseDate)
                            {
                                using (var upd = Repo.Instance.AniDB_Anime.BeginAddOrUpdate(() => anime))
                                {
                                    upd.Entity.AirDate = cal.ReleaseDate;
                                    upd.Commit();
                                }

                                SVR_AnimeSeries ser = Repo.Instance.AnimeSeries.GetByAnimeID(anime.AnimeID);
                                if (ser != null)
                                    Repo.Instance.AnimeSeries.Touch(() => ser, (true, false, false, false));
                            }
                        }
                    }
                    else
                    {
                        cmds.Add(new CmdAniDBGetAnimeHTTP(cal.AnimeID, true, false));
                    }
                }

                ReportUpdate(progress,90);
                if (cmds.Count > 0)
                    Queue.Instance.AddRange(cmds);
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing Command AniDB.GetCalendar: {ex}", ex);
            }
        }
    }
}