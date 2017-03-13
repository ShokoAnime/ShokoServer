using System;
using System.Xml;
using Shoko.Models.Azure;
using Shoko.Server.Repositories.Direct;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandRequest_WebCacheSendXRefAniDBOther : CommandRequestImplementation, ICommandRequest
    {
        public int CrossRef_AniDB_OtherID { get; set; }

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
                    queueState = QueueStateEnum.WebCacheSendXRefAniDBOther,
                    extraParams = new string[] {CrossRef_AniDB_OtherID.ToString()}
                };
            }
        }

        public CommandRequest_WebCacheSendXRefAniDBOther()
        {
        }

        public CommandRequest_WebCacheSendXRefAniDBOther(int xrefID)
        {
            this.CrossRef_AniDB_OtherID = xrefID;
            this.CommandType = (int) CommandRequestType.WebCache_SendXRefAniDBOther;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            try
            {
                CrossRef_AniDB_Other xref = RepoFactory.CrossRef_AniDB_Other.GetByID(CrossRef_AniDB_OtherID);
                if (xref == null) return;

                AzureWebAPI.Send_CrossRefAniDBOther(xref);
            }
            catch (Exception ex)
            {
                logger.ErrorException(
                    "Error processing CommandRequest_WebCacheSendXRefAniDBOther: {0}" + ex.ToString(), ex);
                return;
            }
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_WebCacheSendXRefAniDBOther{0}", CrossRef_AniDB_OtherID);
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
                this.CrossRef_AniDB_OtherID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendXRefAniDBOther",
                        "CrossRef_AniDB_OtherID"));
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest();
            cq.CommandID = this.CommandID;
            cq.CommandType = this.CommandType;
            cq.Priority = this.Priority;
            cq.CommandDetails = this.ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}