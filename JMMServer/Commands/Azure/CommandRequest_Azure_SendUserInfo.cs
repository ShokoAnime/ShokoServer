using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using JMMServer.Providers.Azure;
using System.Xml;
using JMMDatabase;
using JMMServerModels.DB.Childs;

namespace JMMServer.Commands.Azure
{
    public class CommandRequest_Azure_SendUserInfo : BaseCommandRequest, ICommandRequest
    {
        public string Username { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return "Sending anonymous usage data to azure";
			}
		}

		public CommandRequest_Azure_SendUserInfo()
		{
		}

		public CommandRequest_Azure_SendUserInfo(string username)
		{
			this.Username = username;
            this.CommandType = CommandRequestType.Azure_SendUserInfo;
			this.Priority = DefaultPriority;
            this.JMMUserId = Store.JmmUserRepo.GetMasterUser().Id;
            this.Id= $"CommandRequest_Azure_SendUserInfo_{this.Username}";
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

    }
}
