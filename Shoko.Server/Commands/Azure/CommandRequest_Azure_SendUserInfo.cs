using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.Azure;

namespace Shoko.Server.Commands
{
    public class CommandRequest_Azure_SendUserInfo : CommandRequest
    {
        public virtual string Username { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.SendAnonymousData,
            extraParams = new string[0]
        };

        public CommandRequest_Azure_SendUserInfo()
        {
        }

        public CommandRequest_Azure_SendUserInfo(string username)
        {
            Username = username;
            CommandType = (int) CommandRequestType.Azure_SendUserInfo;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            try
            {
                AzureWebAPI.Send_UserInfo();
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_Azure_SendUserInfo: {0} ", ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_Azure_SendUserInfo_{Username}";
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
                Username = TryGetProperty(docCreator, "CommandRequest_Azure_SendUserInfo", "Username");
            }

            return true;
        }
    }
}