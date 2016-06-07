using System;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Providers.Azure;

namespace JMMServer.Commands.WebCache
{
    public class CommandRequest_WebCacheDeleteXRefAniDBMAL : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_WebCacheDeleteXRefAniDBMAL()
        {
        }

        public CommandRequest_WebCacheDeleteXRefAniDBMAL(int animeID, int epType, int epNumber)
        {
            AnimeID = animeID;
            StartEpisodeType = epType;
            StartEpisodeNumber = epNumber;
            CommandType = (int)CommandRequestType.WebCache_DeleteXRefAniDBMAL;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int AnimeID { get; set; }
        public int StartEpisodeType { get; set; }
        public int StartEpisodeNumber { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get { return string.Format("Deleting cross ref for Anidb to MAL from web cache: {0}", AnimeID); }
        }

        public override void ProcessCommand()
        {
            try
            {
                AzureWebAPI.Delete_CrossRefAniDBMAL(AnimeID, StartEpisodeType, StartEpisodeNumber);
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error processing CommandRequest_WebCacheDeleteXRefAniDBMAL: {0}" + ex, ex);
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBMAL", "AnimeID"));
                StartEpisodeType =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBMAL", "StartEpisodeType"));
                StartEpisodeNumber =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBMAL",
                        "StartEpisodeNumber"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_WebCacheDeleteXRefAniDBMAL{0}", AnimeID);
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