using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Providers.Azure;

namespace JMMServer.Commands.Azure
{
    public class CommandRequest_Azure_SendUserInfo : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_Azure_SendUserInfo()
        {
        }

        public CommandRequest_Azure_SendUserInfo(string username)
        {
            Username = username;
            CommandType = (int)CommandRequestType.Azure_SendUserInfo;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public string Username { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return Resources.Command_SendAnonymousData;
            }
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
                Username = TryGetProperty(docCreator, "CommandRequest_Azure_SendUserInfo", "Username");
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_Azure_SendUserInfo_{0}", Username);
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