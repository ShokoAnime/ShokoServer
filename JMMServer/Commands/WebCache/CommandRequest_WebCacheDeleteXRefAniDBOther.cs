using System;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Providers.Azure;

namespace JMMServer.Commands
{
    public class CommandRequest_WebCacheDeleteXRefAniDBOther : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_WebCacheDeleteXRefAniDBOther()
        {
        }

        public CommandRequest_WebCacheDeleteXRefAniDBOther(int animeID, CrossRefType xrefType)
        {
            AnimeID = animeID;
            CommandType = (int)CommandRequestType.WebCache_DeleteXRefAniDBOther;
            CrossRefType = (int)xrefType;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int AnimeID { get; set; }
        public int CrossRefType { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get { return string.Format("Deleting cross ref for Anidb to Other from web cache: {0}", AnimeID); }
        }

        public override void ProcessCommand()
        {
            try
            {
                AzureWebAPI.Delete_CrossRefAniDBOther(AnimeID, (CrossRefType)CrossRefType);
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error processing CommandRequest_WebCacheDeleteXRefAniDBOther: {0}" + ex, ex);
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBOther", "AnimeID"));
                CrossRefType =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBOther", "CrossRefType"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_WebCacheDeleteXRefAniDBOther_{0}_{1}", AnimeID, CrossRefType);
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