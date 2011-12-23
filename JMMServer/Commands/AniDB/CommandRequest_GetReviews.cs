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
	public class CommandRequest_GetReviews : CommandRequestImplementation, ICommandRequest
	{
		public int AnimeID { get; set; }
		public bool ForceRefresh { get; set; }

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Getting review info from UDP API for Anime: {0}", AnimeID);
			}
		}

		public CommandRequest_GetReviews()
		{
		}

		public CommandRequest_GetReviews(int animeid, bool forced)
		{
			this.AnimeID = animeid;
			this.ForceRefresh = forced;
			this.CommandType = (int)CommandRequestType.AniDB_GetReviews;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_GetReviews: {0}", AnimeID);

			try
			{
				return;

				// we will always assume that an anime was downloaded via http first
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				AniDB_Anime anime = repAnime.GetByAnimeID(AnimeID);

				if (anime != null)
				{
					// reviews count will be 0 when the anime is only downloaded via HTTP
					if (ForceRefresh || anime.AnimeReviews.Count == 0)
						anime = JMMService.AnidbProcessor.GetAnimeInfoUDP(AnimeID, true);

					foreach (AniDB_Anime_Review animeRev in anime.AnimeReviews)
					{
						JMMService.AnidbProcessor.GetReviewUDP(animeRev.ReviewID);
					}
					
				}

			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_GetReviews: {0} - {1}", AnimeID, ex.ToString());
				return;
			}
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_GetReviews_{0}", this.AnimeID);
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
				this.AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetReviews", "AnimeID"));
				this.ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetReviews", "ForceRefresh"));
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
