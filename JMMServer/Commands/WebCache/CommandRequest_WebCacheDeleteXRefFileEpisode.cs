using System;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Providers.Azure;

namespace JMMServer.Commands
{
    public class CommandRequest_WebCacheDeleteXRefFileEpisode : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_WebCacheDeleteXRefFileEpisode()
        {
        }

        public CommandRequest_WebCacheDeleteXRefFileEpisode(string hash, int aniDBEpisodeID)
        {
            Hash = hash;
            EpisodeID = aniDBEpisodeID;
            CommandType = (int)CommandRequestType.WebCache_DeleteXRefFileEpisode;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public string Hash { get; set; }
        public int EpisodeID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                return string.Format("Deleting cross ref for file to episode to web cache: {0}-{1}", Hash, EpisodeID);
            }
        }

        public override void ProcessCommand()
        {
            try
            {
                AzureWebAPI.Delete_CrossRefFileEpisode(Hash);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_WebCacheDeleteXRefFileEpisode: {0}-{1} - {2}", Hash,
                    EpisodeID, ex.ToString());
            }
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
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                Hash = TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefFileEpisode", "Hash");
                EpisodeID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefFileEpisode", "EpisodeID"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_WebCacheDeleteXRefFileEpisode-{0}-{1}", Hash, EpisodeID);
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest();
            cq.CommandID = CommandID;
            cq.CommandType = CommandType;
            cq.Priority = Priority;
            cq.CommandDetails = ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}