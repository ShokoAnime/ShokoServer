using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using JMMServer.Repositories;
using JMMServer.WebCache;
using System.Xml;

namespace JMMServer.Commands
{
	public class CommandRequest_WebCacheSendFileHash : CommandRequestImplementation, ICommandRequest
	{
		public int VideoLocalID { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Sending file hash to web cache: {0}", VideoLocalID);
			}
		}

		public CommandRequest_WebCacheSendFileHash()
		{
		}

		public CommandRequest_WebCacheSendFileHash(int vidLocalID)
		{
			this.VideoLocalID = vidLocalID;
			this.CommandType = (int)CommandRequestType.WebCache_SendFileHash;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			
			try
			{
				VideoLocalRepository repVids = new VideoLocalRepository();
				VideoLocal vlocal = repVids.GetByID(VideoLocalID);
				if (vlocal == null) return;

				XMLService.Send_FileHash(vlocal);
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_WebCacheSendFileHash: {0} - {1}", VideoLocalID, ex.ToString());
				return;
			}
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_WebCacheSendFileHash_{0}", this.VideoLocalID);
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
				this.VideoLocalID = int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendFileHash", "VideoLocalID"));
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
