using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMServer.Entities;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    public class CommandRequest_RefreshAnime : BaseCommandRequest, ICommandRequest
    {
        public int AnimeID { get; set; }
        public CommandRequest_RefreshAnime(int animeID)
        {
            AnimeID = animeID;

            this.CommandType = (int) CommandRequestType.Refresh_AnimeStats;
            this.Priority = (int)DefaultPriority;
            GenerateCommandID();
        }
        public CommandRequest_RefreshAnime()
        {

        }



        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority5; }
        }

        public string PrettyDescription
        {
            get
            {
                return string.Format("Refreshing Anime Stats: {0}", AnimeID);
            }
        }

        public override void ProcessCommand()
        {
            AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
            AnimeSeries ser = repSeries.GetByAnimeID(AnimeID);
            if (ser!=null)
                ser.QueueUpdateStats();
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_RefreshAnime_{0}", this.AnimeID);
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            this.CommandID = cq.CommandID;
            this.CommandRequestID = cq.CommandRequestID;
            this.CommandType = cq.CommandType;
            this.Priority = cq.Priority;
            this.CommandDetails = cq.CommandDetails;
            this.DateTimeUpdated = cq.DateTimeUpdated;
            AnimeID = int.Parse(cq.CommandDetails);
            return true;
        }


        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();
            CommandRequest cq = new CommandRequest();
            cq.CommandID = this.CommandID;
            cq.CommandType = this.CommandType;
            cq.Priority = this.Priority;
            cq.CommandDetails = AnimeID.ToString();
            cq.DateTimeUpdated = DateTime.Now;
            return cq;
        }
    }
}
