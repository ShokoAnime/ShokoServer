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
    [Command(CommandRequestType.WebCache_DeleteXRefFileEpisode)]
    public class CommandRequest_WebCacheDeleteXRefFileEpisode : CommandRequestImplementation
    {
        public string Hash { get; set; }
        public int EpisodeID { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.WebCacheDeleteXRefFileEpisode,
            extraParams = new[] {Hash, EpisodeID.ToString()}
        };

        public CommandRequest_WebCacheDeleteXRefFileEpisode()
        {
        }

        public CommandRequest_WebCacheDeleteXRefFileEpisode(string hash, int aniDBEpisodeID)
        {
            Hash = hash;
            EpisodeID = aniDBEpisodeID;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            try
            {
                AzureWebAPI.Delete_CrossRefFileEpisode(Hash);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error processing CommandRequest_WebCacheDeleteXRefFileEpisode: {0}-{1} - {2}", Hash,
                    EpisodeID,
                    ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_WebCacheDeleteXRefFileEpisode-{Hash}-{EpisodeID}";
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
                Hash = TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefFileEpisode", "Hash");
                EpisodeID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefFileEpisode", "EpisodeID"));
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