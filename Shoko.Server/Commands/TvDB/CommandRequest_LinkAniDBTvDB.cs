using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Server.Providers.TvDB;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_LinkAniDBTvDB : CommandRequest
    {
        public virtual int animeID { get; set; }
        public virtual EpisodeType aniEpType { get; set; }
        public virtual int aniEpNumber { get; set; }
        public virtual int tvDBID { get; set; }
        public virtual int tvSeasonNumber { get; set; }
        public virtual int tvEpNumber { get; set; }
        public virtual bool excludeFromWebCache { get; set; }
        public virtual bool additiveLink { get; set; }

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

            CommandType = (int) CommandRequestType.LinkAniDBTvDB;
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
    }
}