using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using JMMServer.Repositories;
using JMMServer.WebCache;
using System.Xml;

namespace JMMServer.Commands.WebCache
{
	public class CommandRequest_WebCacheSendAniDB_File : CommandRequestImplementation, ICommandRequest
	{
		public int AniDB_FileID { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Sending file hash to web cache: {0}", AniDB_FileID);
			}
		}

		public CommandRequest_WebCacheSendAniDB_File()
		{
		}

		public CommandRequest_WebCacheSendAniDB_File(int aniDB_FileID)
		{
			this.AniDB_FileID = aniDB_FileID;
			this.CommandType = (int)CommandRequestType.WebCache_SendAniDB_File;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			
			try
			{
				AniDB_FileRepository rep = new AniDB_FileRepository();
				AniDB_File anifile = rep.GetByID(AniDB_FileID);
				if (anifile == null) return;

				// skip if the video data is not populated
				if (anifile.File_VideoResolution.Equals("0x0", StringComparison.InvariantCultureIgnoreCase)) return;

				XMLService.Send_AniDB_File(anifile);
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_WebCacheSendAniDB_File: {0} - {1}", AniDB_FileID, ex.ToString());
				return;
			}
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_WebCacheSendAniDB_File_{0}", this.AniDB_FileID);
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
				this.AniDB_FileID = int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendAniDB_File", "AniDB_FileID"));
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
