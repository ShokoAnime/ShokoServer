using System;
using System.Xml;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Models.TvDB;
using Shoko.Server.Providers.TvDB;

namespace Shoko.Server.Commands.TvDB
{
    [Serializable]
    public class CommandRequest_LinkAniDBTvDB : CommandRequestImplementation, ICommandRequest
    {
        public int animeID;
        public enEpisodeType aniEpType;
        public int aniEpNumber;
        public int tvDBID;
        public int tvSeasonNumber;
        public int tvEpNumber;
        public bool excludeFromWebCache;
        public bool additiveLink;

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority8; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct()
                {
                    queueState = QueueStateEnum.LinkAniDBTvDB,
                    extraParams = new string[] {animeID.ToString()}
                };
            }
        }

        public CommandRequest_LinkAniDBTvDB()
        {
        }

        public CommandRequest_LinkAniDBTvDB(int animeID, enEpisodeType aniEpType, int aniEpNumber, int tvDBID,
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

            this.CommandType = (int) CommandRequestType.LinkAniDBTvDB;
            this.Priority = (int) DefaultPriority;

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
                    ex.ToString());
                return;
            }
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_LinkAniDBTvDB{0}{1}{2}{3}{4}{5}", this.animeID,
                this.aniEpType, this.aniEpNumber, this.tvDBID, this.tvSeasonNumber, this.tvEpNumber);
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
                this.animeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "animeID"));
                this.aniEpType = (enEpisodeType) Enum.Parse(typeof(enEpisodeType),
                    TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "aniEpType"));
                this.aniEpNumber = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "aniEpNumber"));
                this.tvDBID = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "tvDBID"));
                this.tvSeasonNumber = int.Parse(
                    TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "tvSeasonNumber"));
                this.tvEpNumber = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "tvEpNumber"));
                this.excludeFromWebCache = bool.Parse(
                    TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "excludeFromWebCache"));
                this.additiveLink = bool.Parse(
                    TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "additiveLink"));
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