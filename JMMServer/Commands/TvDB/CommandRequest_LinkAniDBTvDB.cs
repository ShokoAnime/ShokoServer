using JMMServer.Entities;
using JMMServer.Providers.TvDB;
using System;
using System.Xml;

namespace JMMServer.Commands.TvDB
{
    [Serializable]
    public class CommandRequest_LinkAniDBTvDB : CommandRequestImplementation, ICommandRequest
    {

        public int animeID;
        public AniDBAPI.enEpisodeType aniEpType;
        public int aniEpNumber;
        public int tvDBID;
        public int tvSeasonNumber;
        public int tvEpNumber;
        public bool excludeFromWebCache;

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority8; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct() { queueState = QueueStateEnum.LinkAniDBTvDB, extraParams = new string[] { animeID.ToString() } };
            }
        }

        public CommandRequest_LinkAniDBTvDB()
        {
        }

        public CommandRequest_LinkAniDBTvDB(int animeID, AniDBAPI.enEpisodeType aniEpType, int aniEpNumber, int tvDBID,
            int tvSeasonNumber, int tvEpNumber, bool excludeFromWebCache)
        {
            this.animeID = animeID;
            this.aniEpType = aniEpType;
            this.aniEpNumber = aniEpNumber;
            this.tvDBID = tvDBID;
            this.tvSeasonNumber = tvSeasonNumber;
            this.tvEpNumber = tvEpNumber;
            this.excludeFromWebCache = excludeFromWebCache;

            this.CommandType = (int)CommandRequestType.LinkAniDBTvDB;
            this.Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_LinkAniDBTvDB: {0}", animeID);

            try
            {
                TvDBHelper.LinkAniDBTvDB(animeID, aniEpType, aniEpNumber, tvDBID, tvSeasonNumber, tvEpNumber, excludeFromWebCache);
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
            this.CommandID = string.Format("CommandRequest_LinkAniDBTvDB{0}{1}{2}{3}{4}{5}", this.animeID,this.aniEpType,this.aniEpNumber,this.tvDBID,this.tvSeasonNumber,this.tvEpNumber);
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
                this.aniEpType = (AniDBAPI.enEpisodeType)Enum.Parse(typeof(AniDBAPI.enEpisodeType), TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "aniEpType"));
                this.aniEpNumber = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "aniEpNumber"));
                this.tvDBID = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "tvDBID"));
                this.tvSeasonNumber = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "tvSeasonNumber"));
                this.tvEpNumber = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "tvEpNumber"));
                this.excludeFromWebCache = bool.Parse(TryGetProperty(docCreator, "CommandRequest_LinkAniDBTvDB", "excludeFromWebCache"));
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest();
            cq.CommandID = this.CommandID;
            cq.CommandType = this.CommandType;
            cq.Priority = this.Priority;
            cq.CommandDetails = this.ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}
