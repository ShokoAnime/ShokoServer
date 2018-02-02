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
    public class CommandRequest_GetCalendar : CommandRequest_AniDBBase
    {
        public virtual bool ForceRefresh { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority5;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct {queueState = QueueStateEnum.GetCalendar, extraParams = new string[0]};

        public CommandRequest_GetCalendar()
        {
        }

        public CommandRequest_GetCalendar(bool forced)
        {
            ForceRefresh = forced;
            CommandType = (int) CommandRequestType.AniDB_GetCalendar;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_GetCalendar");

            try
            {
                // we will always assume that an anime was downloaded via http first

                using (var upd = Repo.ScheduledUpdate.BeginAddOrUpdate(()=>Repo.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBCalendar)))
                {
                    upd.Entity.UpdateType = (int) ScheduledUpdateType.AniDBCalendar;
                    upd.Entity.UpdateDetails = string.Empty;
                    int freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_Calendar_UpdateFrequency);

                    // if we have run this in the last 12 hours and are not forcing it, then exit
                    TimeSpan tsLastRun = DateTime.Now - upd.Entity.LastUpdate;
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!ForceRefresh) return;
                    }
                    upd.Entity.LastUpdate = DateTime.Now;
                    upd.Commit();
                }


                CalendarCollection colCalendars = ShokoService.AnidbProcessor.GetCalendarUDP();
                if (colCalendars == null || colCalendars.Calendars == null)
                {
                    logger.Error("Could not get calendar from AniDB");
                    return;
                }

                foreach (Calendar cal in colCalendars.Calendars)
                {
                    AniDB_Anime anime=null;
                    using (var upd = Repo.AniDB_Anime.BeginAddOrUpdate(() => Repo.AniDB_Anime.GetByAnimeID(cal.AnimeID)))
                    {

                        if (upd.Original != null)
                        {
                            // don't update if the local data is less 2 days old
                            TimeSpan ts = DateTime.Now - upd.Entity.DateTimeUpdated;
                            if (ts.TotalDays >= 2)
                            {
                                upd.Release();
                                CommandRequest_GetAnimeHTTP cmdAnime = new CommandRequest_GetAnimeHTTP(cal.AnimeID, true, false);
                                cmdAnime.Save();
                            }
                            else
                            {
                                // update the release date even if we don't update the anime record
                                if (upd.Entity.AirDate != cal.ReleaseDate)
                                {
                                    upd.Entity.AirDate = cal.ReleaseDate;
                                    anime = upd.Commit();
                                    upd.Release();                                    
                                    Repo.AnimeSeries.Touch(()=> Repo.AnimeSeries.GetByAnimeID(anime.AnimeID),(true, false, false, false));
                                }
                            }
                        }
                        else
                        {
                            upd.Release();
                            CommandRequest_GetAnimeHTTP cmdAnime = new CommandRequest_GetAnimeHTTP(cal.AnimeID, true, false);
                            cmdAnime.Save();
                        }
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

        public override bool InitFromDB(Shoko.Models.Server.CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            CommandType = cq.CommandType;
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
    }
}