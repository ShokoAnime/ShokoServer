using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Models;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Server;

namespace Shoko.Server.Commands.TvDB
{
    [Serializable]
    [Command(CommandRequestType.LinkAniDBTvDB)]
    public class CommandRequest_LinkAniDBTvDB : CommandRequestImplementation
    {
        public int animeID;
        public int tvDBID;
        public bool additiveLink;

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority5;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.LinkAniDBTvDB,
            extraParams = new[] {animeID.ToString()}
        };

        public CommandRequest_LinkAniDBTvDB()
        {
        }

        public CommandRequest_LinkAniDBTvDB(int animeID, int tvDBID, bool additiveLink = false)
        {
            this.animeID = animeID;
            this.tvDBID = tvDBID;
            this.additiveLink = additiveLink;

            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            logger.Info("Processing CommandRequest_LinkAniDBTvDB: {0}", animeID);

            try
            {
                TvDBApiHelper.LinkAniDBTvDB(animeID, tvDBID, additiveLink);
                SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_LinkAniDBTvDB: {0} - {1}", animeID,
                    ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID =
                $"CommandRequest_LinkAniDBTvDB_{animeID}_{tvDBID}";
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
                animeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "animeID"));
                tvDBID = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "tvDBID"));
                additiveLink = bool.Parse(
                    TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "additiveLink"));
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
