using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandRequest_WebCacheSendXRefAniDBOther : CommandRequest
    {
        public virtual int CrossRef_AniDB_OtherID { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.WebCacheSendXRefAniDBOther,
            extraParams = new[] {CrossRef_AniDB_OtherID.ToString()}
        };

        public CommandRequest_WebCacheSendXRefAniDBOther()
        {
        }

        public CommandRequest_WebCacheSendXRefAniDBOther(int xrefID)
        {
            CrossRef_AniDB_OtherID = xrefID;
            CommandType = (int) CommandRequestType.WebCache_SendXRefAniDBOther;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            try
            {
                CrossRef_AniDB_Other xref = Repo.CrossRef_AniDB_Other.GetByID(CrossRef_AniDB_OtherID);
                if (xref == null) return;

                AzureWebAPI.Send_CrossRefAniDBOther(xref);
            }
            catch (Exception ex)
            {
                logger.Error(ex,
                    "Error processing CommandRequest_WebCacheSendXRefAniDBOther: {0}" + ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_WebCacheSendXRefAniDBOther{CrossRef_AniDB_OtherID}";
        }

        public override bool InitFromDB(CommandRequest cq)
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
                CrossRef_AniDB_OtherID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendXRefAniDBOther",
                        "CrossRef_AniDB_OtherID"));
            }

            return true;
        }
    }
}