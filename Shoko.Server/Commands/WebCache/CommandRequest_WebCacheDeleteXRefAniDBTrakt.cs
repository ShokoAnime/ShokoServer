using System;
using System.Xml;
using Shoko.Models.Azure;
using Shoko.Models.Server;
using Shoko.Server.Providers.Azure;

namespace Shoko.Server.Commands
{
    public class CommandRequest_WebCacheDeleteXRefAniDBTrakt : CommandRequestImplementation, ICommandRequest
    {
        public int AnimeID { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public string TraktID { get; set; }
        public int TraktSeasonNumber { get; set; }
        public int TraktStartEpisodeNumber { get; set; }

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
                    queueState = QueueStateEnum.WebCacheDeleteXRefAniDBTrakt,
                    extraParams = new string[] {AnimeID.ToString()}
                };
            }
        }

        public CommandRequest_WebCacheDeleteXRefAniDBTrakt()
        {
        }

        public CommandRequest_WebCacheDeleteXRefAniDBTrakt(int animeID, int aniDBStartEpisodeType,
            int aniDBStartEpisodeNumber,
            string traktID,
            int traktSeasonNumber, int traktStartEpisodeNumber)
        {
            this.AnimeID = animeID;
            this.AniDBStartEpisodeType = aniDBStartEpisodeType;
            this.AniDBStartEpisodeNumber = aniDBStartEpisodeNumber;
            this.TraktID = traktID;
            this.TraktSeasonNumber = traktSeasonNumber;
            this.TraktStartEpisodeNumber = traktStartEpisodeNumber;
            this.CommandType = (int) CommandRequestType.WebCache_DeleteXRefAniDBTrakt;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            try
            {
                AzureWebAPI.Delete_CrossRefAniDBTrakt(AnimeID, AniDBStartEpisodeType, AniDBStartEpisodeNumber, TraktID,
                    TraktSeasonNumber, TraktStartEpisodeNumber);
            }
            catch (Exception ex)
            {
                logger.ErrorException(
                    "Error processing CommandRequest_WebCacheDeleteXRefAniDBTrakt: {0}" + ex.ToString(), ex);
                return;
            }
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_WebCacheDeleteXRefAniDBTrakt{0}", AnimeID);
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
                this.AnimeID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt", "AnimeID"));
                this.AniDBStartEpisodeType =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt",
                        "AniDBStartEpisodeType"));
                this.AniDBStartEpisodeNumber =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt",
                        "AniDBStartEpisodeNumber"));
                this.TraktID = TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt", "TraktID");
                this.TraktSeasonNumber =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt",
                        "TraktSeasonNumber"));
                this.TraktStartEpisodeNumber =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt",
                        "TraktStartEpisodeNumber"));
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