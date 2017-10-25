using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.TvDB;

namespace Shoko.Server.Commands.TvDB
{
    [Serializable]
    [Command(CommandRequestType.LinkAniDBTvDB)]
    public class CommandRequest_LinkAniDBTvDB : CommandRequestImplementation
    {
        public int animeID;
        public EpisodeType aniEpType;
        public int aniEpNumber;
        public int tvDBID;
        public int tvSeasonNumber;
        public int tvEpNumber;
        public bool excludeFromWebCache;
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

        public CommandRequest_LinkAniDBTvDB(int animeID, EpisodeType aniEpType, int aniEpNumber, int tvDBID,
            int tvSeasonNumber, int tvEpNumber, bool excludeFromWebCache, bool additiveLink = false)
        {
            this.animeID = animeID;
            this.aniEpType = aniEpType;
            this.aniEpNumber = aniEpNumber;
            this.tvDBID = tvDBID;
            this.tvSeasonNumber = tvSeasonNumber;
            this.tvEpNumber = tvEpNumber;
            this.excludeFromWebCache = excludeFromWebCache;
            this.additiveLink = additiveLink;

            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_LinkAniDBTvDB: {0}", animeID);

            try
            {
                TvDBApiHelper.LinkAniDBTvDB(animeID, aniEpType, aniEpNumber, tvDBID, tvSeasonNumber, tvEpNumber,
                    excludeFromWebCache, additiveLink);
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
                $"CommandRequest_LinkAniDBTvDB_{animeID}_{aniEpType}_{aniEpNumber}_{tvDBID}_{tvSeasonNumber}_{tvEpNumber}";
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
                aniEpType = (EpisodeType) Enum.Parse(typeof(EpisodeType),
                    TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "aniEpType"));
                aniEpNumber = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "aniEpNumber"));
                tvDBID = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "tvDBID"));
                tvSeasonNumber = int.Parse(
                    TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "tvSeasonNumber"));
                tvEpNumber = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "tvEpNumber"));
                excludeFromWebCache = bool.Parse(
                    TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "excludeFromWebCache"));
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