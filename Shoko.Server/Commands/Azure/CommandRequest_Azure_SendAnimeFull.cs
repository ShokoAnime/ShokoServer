using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandRequest_Azure_SendAnimeFull : CommandRequest
    {
        public virtual int AnimeID { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.SendAnimeFull,
            extraParams = new[] {AnimeID.ToString()}
        };

        public CommandRequest_Azure_SendAnimeFull()
        {
        }

        public CommandRequest_Azure_SendAnimeFull(int animeID)
        {
            AnimeID = animeID;
            CommandType = (int) CommandRequestType.Azure_SendAnimeFull;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            try
            {
                bool process =
                    ServerSettings.AniDB_Username.Equals("jonbaby", StringComparison.InvariantCultureIgnoreCase) ||
                    ServerSettings.AniDB_Username.Equals("jmediamanager",
                        StringComparison.InvariantCultureIgnoreCase) ||
                    ServerSettings.AniDB_Username.Equals("jmmtesting", StringComparison.InvariantCultureIgnoreCase);

                if (!process) return;

                SVR_AniDB_Anime anime = Repo.AniDB_Anime.GetByAnimeID(AnimeID);
                if (anime == null) return;

                if (anime.AllTags.ToUpper().Contains("18 RESTRICTED")) return;

                AzureWebAPI.Send_AnimeFull(anime);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_Azure_SendAnimeFull: {0} - {1}", AnimeID, ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_Azure_SendAnimeFull_{AnimeID}";
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_Azure_SendAnimeFull", "AnimeID"));
            }

            return true;
        }
    }
}