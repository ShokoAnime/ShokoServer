using System;
using System.Globalization;
using System.Threading;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    public class CommandRequest_RefreshAnime : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_RefreshAnime(int animeID)
        {
            AnimeID = animeID;

            CommandType = (int)CommandRequestType.Refresh_AnimeStats;
            Priority = (int)DefaultPriority;
            GenerateCommandID();
        }

        public CommandRequest_RefreshAnime()
        {
        }

        public int AnimeID { get; set; }


        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority5; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_Refresh, AnimeID);
            }
        }

        public override void ProcessCommand()
        {
            var repSeries = new AnimeSeriesRepository();
            var ser = repSeries.GetByAnimeID(AnimeID);
            if (ser != null)
                ser.UpdateStats(true, true, true);
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            CommandType = cq.CommandType;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;
            AnimeID = int.Parse(cq.CommandDetails);
            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_RefreshAnime_{0}", AnimeID);
        }


        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();
            var cq = new CommandRequest();
            cq.CommandID = CommandID;
            cq.CommandType = CommandType;
            cq.Priority = Priority;
            cq.CommandDetails = AnimeID.ToString();
            cq.DateTimeUpdated = DateTime.Now;
            return cq;
        }
    }
}