using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Server;

namespace Shoko.Server.Commands
{
    [Command(CommandRequestType.WebCache_DeleteXRefAniDBTrakt)]
    public class CommandRequest_WebCacheDeleteXRefAniDBTrakt : CommandRequestImplementation
    {
        public int AnimeID { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public string TraktID { get; set; }
        public int TraktSeasonNumber { get; set; }
        public int TraktStartEpisodeNumber { get; set; }

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
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            try
            {
                AzureWebAPI.Delete_CrossRefAniDBTrakt(AnimeID, AniDBStartEpisodeType, AniDBStartEpisodeNumber, TraktID,
                    TraktSeasonNumber, TraktStartEpisodeNumber);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "Error processing CommandRequest_WebCacheDeleteXRefAniDBTrakt: {0}" + ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_WebCacheDeleteXRefAniDBTrakt{AnimeID}";
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