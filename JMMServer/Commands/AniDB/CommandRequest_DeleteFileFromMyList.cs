using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_DeleteFileFromMyList : CommandRequestImplementation, ICommandRequest
	{
		public string Hash { get; set; }
		public long FileSize { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority7; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Deleting file from MyList: {0}", Hash);
			}
		}

		public CommandRequest_DeleteFileFromMyList()
		{
		}

		public CommandRequest_DeleteFileFromMyList(string hash, long fileSize)
		{
			this.Hash = hash;
			this.FileSize = fileSize;
			this.CommandType = (int)CommandRequestType.AniDB_DeleteFileUDP;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_DeleteFileFromMyList: {0}", Hash);

			try
			{
				JMMService.AnidbProcessor.DeleteFileFromMyList(Hash, FileSize);
				logger.Info("Deleting file from list: {0}", Hash);
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_AddFileToMyList: {0} - {1}", Hash, ex.ToString());
				return;
			}
		}

		/// <summary>
		/// This should generate a unique key for a command
		/// It will be used to check whether the command has already been queued before adding it
		/// </summary>
		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_DeleteFileFromMyList{0}", Hash);
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
				this.Hash = TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", "Hash");
				this.FileSize = long.Parse(TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", "FileSize"));
			}

			if (this.Hash.Trim().Length > 0)
				return true;
			else
				return false;
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
