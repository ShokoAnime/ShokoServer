using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.Azure;

namespace Shoko.Server.Commands.WebCache
{
    public class CommandRequest_WebCacheDeleteXRefAniDBMAL : CommandRequestImplementation, ICommandRequest
    {
        public int AnimeID { get; set; }
        public int StartEpisodeType { get; set; }
        public int StartEpisodeNumber { get; set; }

        public CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.WebCacheDeleteXRefAniDBMAL,
            extraParams = new[] {AnimeID.ToString()}
        };

        public CommandRequest_WebCacheDeleteXRefAniDBMAL()
        {
        }

        public CommandRequest_WebCacheDeleteXRefAniDBMAL(int animeID, int epType, int epNumber)
        {
            AnimeID = animeID;
            StartEpisodeType = epType;
            StartEpisodeNumber = epNumber;
            CommandType = (int) CommandRequestType.WebCache_DeleteXRefAniDBMAL;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            try
            {
                AzureWebAPI.Delete_CrossRefAniDBMAL(AnimeID, StartEpisodeType, StartEpisodeNumber);
            }
            catch (Exception ex)
            {
                logger.Error(ex,
                    "Error processing CommandRequest_WebCacheDeleteXRefAniDBMAL: {0}" + ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_WebCacheDeleteXRefAniDBMAL{AnimeID}";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
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
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBMAL", "AnimeID"));
                StartEpisodeType =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBMAL",
                        "StartEpisodeType"));
                StartEpisodeNumber =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBMAL",
                        "StartEpisodeNumber"));
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