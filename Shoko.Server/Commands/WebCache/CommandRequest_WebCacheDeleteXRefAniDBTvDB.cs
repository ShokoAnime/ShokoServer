using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.Azure;

namespace Shoko.Server.Commands
{
    [Command(CommandRequestType.WebCache_DeleteXRefAniDBTvDB)]
    public class CommandRequest_WebCacheDeleteXRefAniDBTvDB : CommandRequestImplementation
    {
        public virtual int AnimeID { get; set; }
        public virtual int AniDBStartEpisodeType { get; set; }
        public virtual int AniDBStartEpisodeNumber { get; set; }
        public virtual int TvDBID { get; set; }
        public virtual int TvDBSeasonNumber { get; set; }
        public virtual int TvDBStartEpisodeNumber { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.WebCacheDeleteXRefAniDBTvDB,
            extraParams = new[] {AnimeID.ToString()}
        };

        public CommandRequest_WebCacheDeleteXRefAniDBTvDB()
        {
        }

        public CommandRequest_WebCacheDeleteXRefAniDBTvDB(int animeID, int aniDBStartEpisodeType,
            int aniDBStartEpisodeNumber,
            int tvDBID,
            int tvDBSeasonNumber, int tvDBStartEpisodeNumber)
        {
            AnimeID = animeID;
            AniDBStartEpisodeType = aniDBStartEpisodeType;
            AniDBStartEpisodeNumber = aniDBStartEpisodeNumber;
            TvDBID = tvDBID;
            TvDBSeasonNumber = tvDBSeasonNumber;
            TvDBStartEpisodeNumber = tvDBStartEpisodeNumber;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            try
            {
                AzureWebAPI.Delete_CrossRefAniDBTvDB(AnimeID, AniDBStartEpisodeType, AniDBStartEpisodeNumber, TvDBID,
                    TvDBSeasonNumber, TvDBStartEpisodeNumber);
            }
            catch (Exception ex)
            {
                logger.Error(ex,
                    "Error processing CommandRequest_WebCacheDeleteXRefAniDBTvDB: {0}" + ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_WebCacheDeleteXRefAniDBTvDB{AnimeID}";
        }

        public override bool InitFromDB(Shoko.Models.Server.CommandRequest cq)
        {
            try
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
                    AnimeID =
                        int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTvDB", "AnimeID"));
                    AniDBStartEpisodeType =
                        int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTvDB",
                            "AniDBStartEpisodeType"));
                    AniDBStartEpisodeNumber =
                        int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTvDB",
                            "AniDBStartEpisodeNumber"));
                    TvDBID =
                        int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTvDB", "TvDBID"));
                    TvDBSeasonNumber =
                        int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTvDB",
                            "TvDBSeasonNumber"));
                    TvDBStartEpisodeNumber =
                        int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTvDB",
                            "TvDBStartEpisodeNumber"));
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex,
                    "Error processing CommandRequest_WebCacheDeleteXRefAniDBTvDB: {0}" + ex);
                return true;
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