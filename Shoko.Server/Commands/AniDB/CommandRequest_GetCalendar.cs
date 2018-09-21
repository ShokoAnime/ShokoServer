using System;
using System.Xml;
using AniDBAPI;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    [Command(CommandRequestType.AniDB_GetCalendar)]
    public class CommandRequest_GetCalendar : CommandRequestImplementation
    {
        public bool ForceRefresh { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority5;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct {queueState = QueueStateEnum.GetCalendar, extraParams = new string[0]};

        public CommandRequest_GetCalendar()
        {
        }

        public CommandRequest_GetCalendar(bool forced)
        {
            ForceRefresh = forced;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_GetCalendar");

            try
            {
                // we will always assume that an anime was downloaded via http first

                ScheduledUpdate sched =
                    Repo.Instance.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBCalendar);

                if (sched != null)
                {
                    int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDb.Calendar_UpdateFrequency);

                    // if we have run this in the last 12 hours and are not forcing it, then exit
                    TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!ForceRefresh) return;
                    }
                }

                using (var upd = Repo.Instance.ScheduledUpdate.BeginAddOrUpdate(() => sched, () => new ScheduledUpdate
                {
                    UpdateType = (int)ScheduledUpdateType.AniDBCalendar,
                    UpdateDetails = string.Empty
                }))
                {
                    upd.Entity.LastUpdate = DateTime.Now;
                    sched = upd.Commit();
                }
                

                CalendarCollection colCalendars = ShokoService.AnidbProcessor.GetCalendarUDP();
                if (colCalendars == null || colCalendars.Calendars == null)
                {
                    logger.Error("Could not get calendar from AniDB");
                    return;
                }
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
                            CommandRequest_GetAnimeHTTP cmdAnime = new CommandRequest_GetAnimeHTTP(cal.AnimeID, true,
                                false);
                            cmdAnime.Save();
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
                        CommandRequest_GetAnimeHTTP cmdAnime =
                            new CommandRequest_GetAnimeHTTP(cal.AnimeID, true, false);
                        cmdAnime.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error processing CommandRequest_GetCalendar: " + ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = "CommandRequest_GetCalendar";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                ForceRefresh = bool.Parse(
                    TryGetProperty(docCreator, "CommandRequest_GetCalendar", "ForceRefresh"));
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest
            {
                CommandID = CommandID,
                CommandType = CommandType,
                Priority = Priority,
                CommandDetails = ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}