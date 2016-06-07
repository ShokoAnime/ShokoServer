using System;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Providers.Azure;
using JMMServer.Repositories;

namespace JMMServer.Commands.WebCache
{
    public class CommandRequest_WebCacheSendXRefAniDBMAL : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_WebCacheSendXRefAniDBMAL()
        {
        }

        public CommandRequest_WebCacheSendXRefAniDBMAL(int xrefID)
        {
            CrossRef_AniDB_MALID = xrefID;
            CommandType = (int)CommandRequestType.WebCache_SendXRefAniDBMAL;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int CrossRef_AniDB_MALID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                return string.Format("Sending cross ref for Anidb to MAL from web cache: {0}", CrossRef_AniDB_MALID);
            }
        }

        public override void ProcessCommand()
        {
            try
            {
                var repCrossRef = new CrossRef_AniDB_MALRepository();
                var xref = repCrossRef.GetByID(CrossRef_AniDB_MALID);
                if (xref == null) return;


                AzureWebAPI.Send_CrossRefAniDBMAL(xref);
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error processing CommandRequest_WebCacheSendXRefAniDBMAL: {0}" + ex, ex);
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
                CrossRef_AniDB_MALID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendXRefAniDBMAL",
                        "CrossRef_AniDB_MALID"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_WebCacheSendXRefAniDBMAL{0}", CrossRef_AniDB_MALID);
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