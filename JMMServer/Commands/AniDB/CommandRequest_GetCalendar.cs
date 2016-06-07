using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_GetCalendar : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_GetCalendar()
        {
        }

        public CommandRequest_GetCalendar(bool forced)
        {
            ForceRefresh = forced;
            CommandType = (int)CommandRequestType.AniDB_GetCalendar;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public bool ForceRefresh { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority7; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_GetCalendar);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_GetCalendar");

            try
            {
                // we will always assume that an anime was downloaded via http first
                var repSched = new ScheduledUpdateRepository();
                var repAnime = new AniDB_AnimeRepository();

                var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.AniDBCalendar);
                if (sched == null)
                {
                    sched = new ScheduledUpdate();
                    sched.UpdateType = (int)ScheduledUpdateType.AniDBCalendar;
                    sched.UpdateDetails = "";
                }
                else
                {
                    var freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_Calendar_UpdateFrequency);

                    // if we have run this in the last 12 hours and are not forcing it, then exit
                    var tsLastRun = DateTime.Now - sched.LastUpdate;
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!ForceRefresh) return;
                    }
                }

                sched.LastUpdate = DateTime.Now;
                repSched.Save(sched);

                var colCalendars = JMMService.AnidbProcessor.GetCalendarUDP();
                if (colCalendars == null || colCalendars.Calendars == null)
                {
                    logger.Error("Could not get calendar from AniDB");
                    return;
                }
                foreach (var cal in colCalendars.Calendars)
                {
                    var anime = repAnime.GetByAnimeID(cal.AnimeID);
                    if (anime != null)
                    {
                        // don't update if the local data is less 2 days old
                        var ts = DateTime.Now - anime.DateTimeUpdated;
                        if (ts.TotalDays >= 2)
                        {
                            var cmdAnime = new CommandRequest_GetAnimeHTTP(cal.AnimeID, true, false);
                            cmdAnime.Save();
                        }
                        else
                        {
                            // update the release date even if we don't update the anime record
                            anime.AirDate = cal.ReleaseDate;
                            repAnime.Save(anime);
                        }
                    }
                    else
                    {
                        var cmdAnime = new CommandRequest_GetAnimeHTTP(cal.AnimeID, true, false);
                        cmdAnime.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error processing CommandRequest_GetCalendar: " + ex, ex);
            }
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
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
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetCalendar", "ForceRefresh"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = "CommandRequest_GetCalendar";
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest();
            cq.CommandID = CommandID;
            cq.CommandType = CommandType;
            cq.Priority = Priority;
            cq.CommandDetails = ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}