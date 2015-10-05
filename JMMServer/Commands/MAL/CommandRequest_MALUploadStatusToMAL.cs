using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.WebCache;
using JMMServer.Providers.MyAnimeList;
using AniDBAPI;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Repositories;

namespace JMMServer.Commands.MAL
{
	[Serializable]
	public class CommandRequest_MALUploadStatusToMAL : BaseCommandRequest, ICommandRequest
	{
		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Uploading watched states to MAL");
			}
		}


		public CommandRequest_MALUploadStatusToMAL()
		{
			this.CommandType = (int)CommandRequestType.MAL_UploadWatchedStates;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_MALUploadStatusToMAL");

			try
			{
				if (string.IsNullOrEmpty(ServerSettings.MAL_Username) || string.IsNullOrEmpty(ServerSettings.MAL_Password))
					return;

				// find the latest eps to update
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				List<AniDB_Anime> animes = repAnime.GetAll();

				foreach (AniDB_Anime anime in animes)
				{
					CommandRequest_MALUpdatedWatchedStatus cmd = new CommandRequest_MALUpdatedWatchedStatus(anime.AnimeID);
					cmd.Save();
				}
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_MALUploadStatusToMAL: {0}", ex.ToString());
				return;
			}
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_MALUploadStatusToMAL");
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
