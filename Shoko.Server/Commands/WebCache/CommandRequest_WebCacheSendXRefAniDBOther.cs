using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Commands
{
    [Command(CommandRequestType.WebCache_SendXRefAniDBOther)]
    public class CommandRequest_WebCacheSendXRefAniDBOther : CommandRequestImplementation
    {
        public int CrossRef_AniDB_OtherID { get; set; }

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
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            try
            {
                CrossRef_AniDB_Other xref = RepoFactory.CrossRef_AniDB_Other.GetByID(CrossRef_AniDB_OtherID);
                if (xref == null) return;

                AzureWebAPI.Send_CrossRefAniDBOther(xref);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "Error processing CommandRequest_WebCacheSendXRefAniDBOther: {0}" + ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_WebCacheSendXRefAniDBOther{CrossRef_AniDB_OtherID}";
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
                CrossRef_AniDB_OtherID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendXRefAniDBOther",
                        "CrossRef_AniDB_OtherID"));
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