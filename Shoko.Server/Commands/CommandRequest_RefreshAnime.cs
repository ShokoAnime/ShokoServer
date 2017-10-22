using System;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
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

            CommandType = (int) CommandRequestType.Refresh_AnimeStats;
            Priority = (int) DefaultPriority;
            GenerateCommandID();
        }

        public CommandRequest_RefreshAnime()
        {
        }


        public CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority8;

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.Refresh,
            extraParams = new[] {AnimeID.ToString()}
        };

        public override void ProcessCommand()
        {
            SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);
            ser?.UpdateStats(true, true, true);
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_RefreshAnime_{AnimeID}";
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


        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();
            CommandRequest cq = new CommandRequest
            {
                CommandID = CommandID,
                CommandType = CommandType,
                Priority = Priority,
                CommandDetails = AnimeID.ToString(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}