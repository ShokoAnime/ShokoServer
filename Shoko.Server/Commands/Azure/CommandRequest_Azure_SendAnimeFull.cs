using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Azure;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands.Azure
{
    public class CommandRequest_Azure_SendAnimeFull : CommandRequestImplementation, ICommandRequest
    {
        public int AnimeID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct()
                {
                    queueState = QueueStateEnum.SendAnimeFull,
                    extraParams = new string[] {AnimeID.ToString()}
                };
            }
        }

        public CommandRequest_Azure_SendAnimeFull()
        {
        }

        public CommandRequest_Azure_SendAnimeFull(int animeID)
        {
            this.AnimeID = animeID;
            this.CommandType = (int) CommandRequestType.Azure_SendAnimeFull;
            this.Priority = (int) DefaultPriority;

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

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
                if (anime == null) return;

                if (anime.AllTags.ToUpper().Contains("18 RESTRICTED")) return;

                AzureWebAPI.Send_AnimeFull(anime);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_Azure_SendAnimeFull: {0} - {1}", AnimeID, ex.ToString());
                return;
            }
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_Azure_SendAnimeFull_{0}", this.AnimeID);
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            this.CommandID = cq.CommandID;
            this.CommandRequestID = cq.CommandRequestID;
            this.CommandType = cq.CommandType;
            this.Priority = cq.Priority;
            this.CommandDetails = cq.CommandDetails;
            this.DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (this.CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(this.CommandDetails);

                // populate the fields
                this.AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_Azure_SendAnimeFull", "AnimeID"));
            }

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
                CommandDetails = this.ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}