using System;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Providers.Azure;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    public class CommandRequest_WebCacheSendXRefFileEpisode : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_WebCacheSendXRefFileEpisode()
        {
        }

        public CommandRequest_WebCacheSendXRefFileEpisode(int crossRef_File_EpisodeID)
        {
            CrossRef_File_EpisodeID = crossRef_File_EpisodeID;
            CommandType = (int)CommandRequestType.WebCache_SendXRefFileEpisode;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int CrossRef_File_EpisodeID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                return string.Format("Sending cross ref for file to episode to web cache: {0}", CrossRef_File_EpisodeID);
            }
        }

        public override void ProcessCommand()
        {
            try
            {
                var repVids = new CrossRef_File_EpisodeRepository();
                var xref = repVids.GetByID(CrossRef_File_EpisodeID);
                if (xref == null) return;

                AzureWebAPI.Send_CrossRefFileEpisode(xref);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_WebCacheSendXRefFileEpisode: {0} - {1}",
                    CrossRef_File_EpisodeID, ex.ToString());
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
                CrossRef_File_EpisodeID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendXRefFileEpisode",
                        "CrossRef_File_EpisodeID"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_WebCacheSendXRefFileEpisode{0}", CrossRef_File_EpisodeID);
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