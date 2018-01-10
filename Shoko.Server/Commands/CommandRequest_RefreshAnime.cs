using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandRequest_RefreshAnime : CommandRequest
    {
        public virtual int AnimeID { get; set; }

        public override string CommandDetails => AnimeID.ToString();

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


        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority8;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.Refresh,
            extraParams = new[] {AnimeID.ToString()}
        };

        public override void ProcessCommand()
        {
            SVR_AnimeSeries ser = Repo.AnimeSeries.GetByAnimeID(AnimeID);
            ser?.UpdateStats(true, true, true);
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_RefreshAnime_{AnimeID}";
        }

        public override bool InitFromDB(Shoko.Models.Server.CommandRequest cq)
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
    }
}