using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;
using System.IO;

namespace Shoko.Server.Commands
{
	[Serializable]
	public class CommandRequest_GetCreator : CommandRequest_AniDBBase
	{
		public int CreatorID { get; set; }
		public bool ForceRefresh { get; set; }

		public override CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Getting creator info from UDP API: {0}", CreatorID);
			}
		}

		public CommandRequest_GetCreator()
		{
		}

		public CommandRequest_GetCreator(int cid, bool forced)
		{
			this.CreatorID = cid;
			this.ForceRefresh = forced;
			this.CommandType = (int)CommandRequestType.AniDB_GetCreator;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_GetCreator: {0}", CreatorID);

			try
			{
				AniDB_CreatorRepository repCreator = new AniDB_CreatorRepository();
				AniDB_Seiyuu creator = repCreator.GetByCreatorID(CreatorID);

				if (ForceRefresh || creator == null)
				{
					// redownload anime details from http ap so we can get an update character list
					creator = JMMService.AnidbProcessor.GetCreatorInfoUDP(CreatorID);
				}

				if (creator != null || !string.IsNullOrEmpty(creator.PosterPath) && !File.Exists(creator.PosterPath))
				{
					CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(creator.AniDB_SeiyuuID, ImageEntityType.AniDB_Creator, false);
					cmd.Save();
				}

			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_GetCreator: {0} - {1}", CreatorID, ex.ToString());
				return;
			}
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_GetCreator_{0}", this.CreatorID);
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
				this.CreatorID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetCreator", "CreatorID"));
				this.ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetCreator", "ForceRefresh"));
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
