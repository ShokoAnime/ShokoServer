using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_GetAnimeHTTP : CommandRequestImplementation, ICommandRequest
	{
		public int AnimeID { get; set; }
		public bool ForceRefresh { get; set; }
		public bool DownloadRelations { get; set; }

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority2; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Getting anime info from HTTP API: {0}", AnimeID);
			}
		}

		public CommandRequest_GetAnimeHTTP()
		{
		}

		public CommandRequest_GetAnimeHTTP(int animeid, bool forced, bool downloadRelations)
		{
			this.AnimeID = animeid;
			this.DownloadRelations = downloadRelations;
			this.ForceRefresh = forced;
			this.CommandType = (int)CommandRequestType.AniDB_GetAnimeHTTP;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_GetAnimeHTTP: {0}", AnimeID);

			try
			{
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				AniDB_Anime anime = JMMService.AnidbProcessor.GetAnimeInfoHTTP(AnimeID, ForceRefresh, DownloadRelations);
				
				// NOTE - related anime are downloaded when the relations are created
				
				// download group status info for this anime
				// the group status will also help us determine missing episodes for a series


				// download reviews
				if (ServerSettings.AniDB_DownloadReviews)
				{
					CommandRequest_GetReviews cmd = new CommandRequest_GetReviews(AnimeID, ForceRefresh);
					cmd.Save();
				}

				// Request an image download

			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_GetAnimeHTTP: {0} - {1}", AnimeID, ex.ToString());
				return;
			}
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_GetAnimeHTTP_{0}", this.AnimeID);
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
				this.AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetAnimeHTTP", "AnimeID"));
				this.DownloadRelations = bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetAnimeHTTP", "DownloadRelations"));
				this.ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetAnimeHTTP", "ForceRefresh"));
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
