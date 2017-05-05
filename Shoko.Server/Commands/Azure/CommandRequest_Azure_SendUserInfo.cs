using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using Shoko.Models.Azure;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.Azure;

namespace Shoko.Server.Commands.Azure
{
    public class CommandRequest_Azure_SendUserInfo : CommandRequestImplementation, ICommandRequest
    {
        public string Username { get; set; }

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
                    queueState = QueueStateEnum.SendAnonymousData,
                    extraParams = new string[0]
                };
            }
        }

        public CommandRequest_Azure_SendUserInfo()
        {
        }

        public CommandRequest_Azure_SendUserInfo(string username)
        {
            this.Username = username;
            this.CommandType = (int) CommandRequestType.Azure_SendUserInfo;
            this.Priority = (int) DefaultPriority;

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
                logger.Error("Error processing CommandRequest_Azure_SendUserInfo: {0} ", ex.ToString());
                return;
            }
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_Azure_SendUserInfo_{0}", this.Username);
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
                this.Username = TryGetProperty(docCreator, "CommandRequest_Azure_SendUserInfo", "Username");
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