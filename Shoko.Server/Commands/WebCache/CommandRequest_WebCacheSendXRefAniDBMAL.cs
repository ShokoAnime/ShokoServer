using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands.WebCache
{
    public class CommandRequest_WebCacheSendXRefAniDBMAL : CommandRequestImplementation, ICommandRequest
    {
        public int CrossRef_AniDB_MALID { get; set; }

        public CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.WebCacheSendXRefAniDBMAL,
            extraParams = new[] {CrossRef_AniDB_MALID.ToString()}
        };

        public CommandRequest_WebCacheSendXRefAniDBMAL()
        {
        }

        public CommandRequest_WebCacheSendXRefAniDBMAL(int xrefID)
        {
            CrossRef_AniDB_MALID = xrefID;
            CommandType = (int) CommandRequestType.WebCache_SendXRefAniDBMAL;
            Priority = (int) DefaultPriority;

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
                logger.Error(ex, "Error processing CommandRequest_WebCacheSendXRefAniDBMAL: {0}" + ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_WebCacheSendXRefAniDBMAL{CrossRef_AniDB_MALID}";
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
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                CrossRef_AniDB_MALID =
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