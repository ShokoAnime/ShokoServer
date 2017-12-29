using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.Azure;

namespace Shoko.Server.Commands
{
    public class CommandRequest_WebCacheDeleteXRefAniDBTrakt : CommandRequest
    {
        public virtual int AnimeID { get; set; }
        public virtual int AniDBStartEpisodeType { get; set; }
        public virtual int AniDBStartEpisodeNumber { get; set; }
        public virtual string TraktID { get; set; }
        public virtual int TraktSeasonNumber { get; set; }
        public virtual int TraktStartEpisodeNumber { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.WebCacheDeleteXRefAniDBTrakt,
            extraParams = new[] {AnimeID.ToString()}
        };

        public CommandRequest_WebCacheDeleteXRefAniDBTrakt()
        {
        }

        public CommandRequest_WebCacheDeleteXRefAniDBTrakt(int animeID, int aniDBStartEpisodeType,
            int aniDBStartEpisodeNumber,
            string traktID,
            int traktSeasonNumber, int traktStartEpisodeNumber)
        {
            AnimeID = animeID;
            AniDBStartEpisodeType = aniDBStartEpisodeType;
            AniDBStartEpisodeNumber = aniDBStartEpisodeNumber;
            TraktID = traktID;
            TraktSeasonNumber = traktSeasonNumber;
            TraktStartEpisodeNumber = traktStartEpisodeNumber;
            CommandType = (int) CommandRequestType.WebCache_DeleteXRefAniDBTrakt;
            Priority = (int) DefaultPriority;

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
                logger.Error(ex,
                    "Error processing CommandRequest_WebCacheDeleteXRefAniDBTrakt: {0}" + ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_WebCacheDeleteXRefAniDBTrakt{AnimeID}";
        }

        public override bool InitFromDB(CommandRequest cq)
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
                AnimeID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt", "AnimeID"));
                AniDBStartEpisodeType =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt",
                        "AniDBStartEpisodeType"));
                AniDBStartEpisodeNumber =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt",
                        "AniDBStartEpisodeNumber"));
                TraktID = TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt", "TraktID");
                TraktSeasonNumber =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt",
                        "TraktSeasonNumber"));
                TraktStartEpisodeNumber =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTrakt",
                        "TraktStartEpisodeNumber"));
            }

            return true;
        }
    }
}