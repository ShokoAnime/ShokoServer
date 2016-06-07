using System;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Providers.Azure;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    public class CommandRequest_WebCacheSendXRefAniDBOther : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_WebCacheSendXRefAniDBOther()
        {
        }

        public CommandRequest_WebCacheSendXRefAniDBOther(int xrefID)
        {
            CrossRef_AniDB_OtherID = xrefID;
            CommandType = (int)CommandRequestType.WebCache_SendXRefAniDBOther;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int CrossRef_AniDB_OtherID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                return string.Format("Sending cross ref for Anidb to Other from web cache: {0}", CrossRef_AniDB_OtherID);
            }
        }

        public override void ProcessCommand()
        {
            try
            {
                var repCrossRef = new CrossRef_AniDB_OtherRepository();
                var xref = repCrossRef.GetByID(CrossRef_AniDB_OtherID);
                if (xref == null) return;

                AzureWebAPI.Send_CrossRefAniDBOther(xref);
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error processing CommandRequest_WebCacheSendXRefAniDBOther: {0}" + ex, ex);
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
                CrossRef_AniDB_OtherID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendXRefAniDBOther",
                        "CrossRef_AniDB_OtherID"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_WebCacheSendXRefAniDBOther{0}", CrossRef_AniDB_OtherID);
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