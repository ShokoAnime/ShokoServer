using System;
using System.Globalization;
using System.Threading;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Repositories.Cached;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandRequest_RefreshAnime : CommandRequestImplementation, ICommandRequest
    {
        public int AnimeID { get; set; }

        public CommandRequest_RefreshAnime(int animeID)
        {
            AnimeID = animeID;

            this.CommandType = (int) CommandRequestType.Refresh_AnimeStats;
            this.Priority = (int) DefaultPriority;
            GenerateCommandID();
        }

        public CommandRequest_RefreshAnime()
        {
        }


        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority5; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct()
                {
                    queueState = QueueStateEnum.Refresh,
                    extraParams = new string[] {AnimeID.ToString()}
                };
            }
        }

        public override void ProcessCommand()
        {
            SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);
            if (ser != null)
                ser.UpdateStats(true, true, true);
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
            CommandRequest cq = new CommandRequest
            {
                CommandID = this.CommandID,
                CommandType = this.CommandType,
                Priority = this.Priority,
                CommandDetails = AnimeID.ToString(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}