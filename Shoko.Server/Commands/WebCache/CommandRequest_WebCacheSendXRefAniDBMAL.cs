using System;
using System.Xml;
using Shoko.Models.Azure;
using Shoko.Models.Queue;
using Shoko.Server.Repositories.Direct;
using Shoko.Models.Server;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands.WebCache
{
    public class CommandRequest_WebCacheSendXRefAniDBMAL : CommandRequestImplementation, ICommandRequest
    {
        public int CrossRef_AniDB_MALID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct()
                {
                    queueState = QueueStateEnum.WebCacheSendXRefAniDBMAL,
                    extraParams = new string[] {CrossRef_AniDB_MALID.ToString()}
                };
            }
        }

        public CommandRequest_WebCacheSendXRefAniDBMAL()
        {
        }

        public CommandRequest_WebCacheSendXRefAniDBMAL(int xrefID)
        {
            this.CrossRef_AniDB_MALID = xrefID;
            this.CommandType = (int) CommandRequestType.WebCache_SendXRefAniDBMAL;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            try
            {
                CrossRef_AniDB_MAL xref = RepoFactory.CrossRef_AniDB_MAL.GetByID(CrossRef_AniDB_MALID);
                if (xref == null) return;


                AzureWebAPI.Send_CrossRefAniDBMAL(xref);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error processing CommandRequest_WebCacheSendXRefAniDBMAL: {0}" + ex.ToString());
                return;
            }
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_WebCacheSendXRefAniDBMAL{0}", CrossRef_AniDB_MALID);
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            this.CommandID = cq.CommandID;
            this.CommandRequestID = cq.CommandRequestID;
            this.CommandType = cq.CommandType;
            this.Priority = cq.Priority;
            this.CommandDetails = cq.CommandDetails;
            this.DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (this.CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(this.CommandDetails);

                // populate the fields
                this.CrossRef_AniDB_MALID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendXRefAniDBMAL",
                        "CrossRef_AniDB_MALID"));
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest
            {
                CommandID = this.CommandID,
                CommandType = this.CommandType,
                Priority = this.Priority,
                CommandDetails = this.ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}