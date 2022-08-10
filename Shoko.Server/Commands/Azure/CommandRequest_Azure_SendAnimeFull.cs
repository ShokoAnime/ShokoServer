using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Commands.Azure
{
    [Command(CommandRequestType.Azure_SendAnimeFull)]
    public class CommandRequest_Azure_SendAnimeFull : CommandRequestImplementation
    {
        public int AnimeID { get; set; }

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
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            try
            {
                bool process = false;

                if (!process) return;

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
                if (anime == null) return;

                if (anime.AllTags.ToUpper().Contains("18 RESTRICTED")) return;

                AzureWebAPI.Send_AnimeFull(anime);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error processing CommandRequest_Azure_SendAnimeFull: {0} - {1}", AnimeID, ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_Azure_SendAnimeFull_{AnimeID}";
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_Azure_SendAnimeFull", "AnimeID"));
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