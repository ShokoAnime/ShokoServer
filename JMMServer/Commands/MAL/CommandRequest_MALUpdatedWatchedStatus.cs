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
	public class CommandRequest_MALUpdatedWatchedStatus : CommandRequestImplementation, ICommandRequest
	{
		public int AnimeID { get; set; }

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Updating status on MAL: {0}", AnimeID);
			}
		}

		public CommandRequest_MALUpdatedWatchedStatus()
		{
		}

		public CommandRequest_MALUpdatedWatchedStatus(int animeID)
		{
			this.AnimeID = animeID;
			this.CommandType = (int)CommandRequestType.MAL_UpdateStatus;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_MALUpdatedWatchedStatus: {0}", AnimeID);

			try
			{
				// find the latest eps to update
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				AniDB_Anime anime = repAnime.GetByAnimeID(AnimeID);
				if (anime == null) return;

				List<CrossRef_AniDB_MAL> crossRefs = anime.CrossRefMAL;
				if (crossRefs == null || crossRefs.Count == 0)
					return;

				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				AnimeSeries ser = repSeries.GetByAnimeID(AnimeID);
				if (ser == null) return;

				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				List<AnimeEpisode> eps = ser.AnimeEpisodes;

				// find the anidb user
				JMMUserRepository repUsers = new JMMUserRepository();
				List<JMMUser> aniDBUsers = repUsers.GetAniDBUsers();
				if (aniDBUsers.Count == 0) return;

				JMMUser user = aniDBUsers[0];

				foreach (CrossRef_AniDB_MAL xref in crossRefs)
				{
					int lastEpNumber = -1;

					foreach (AnimeEpisode ep in eps)
					{
						int epNum = ep.AniDB_Episode.EpisodeNumber;
						if (xref.StartEpisodeType == (int)ep.EpisodeTypeEnum && epNum >= xref.StartEpisodeNumber &&
							epNum <= GetUpperEpisodeLimit(crossRefs, xref))
						{
							AnimeEpisode_User usrRecord = ep.GetUserRecord(user.JMMUserID);
							if (usrRecord != null && usrRecord.WatchedDate.HasValue && epNum > lastEpNumber)
								lastEpNumber = ep.AniDB_Episode.EpisodeNumber;
						}
					}

					if (lastEpNumber > 0)
						MALHelper.UpdateWatchedStatus(AnimeID, (enEpisodeType)xref.StartEpisodeType, lastEpNumber);
					
				}
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_MALUpdatedWatchedStatus: {0} - {1}", AnimeID, ex.ToString());
				return;
			}
		}

		private int GetUpperEpisodeLimit(List<CrossRef_AniDB_MAL> crossRefs, CrossRef_AniDB_MAL xrefBase)
		{
			foreach (CrossRef_AniDB_MAL xref in crossRefs)
			{
				if (xref.StartEpisodeType == xrefBase.StartEpisodeType)
				{
					if (xref.StartEpisodeNumber > xrefBase.StartEpisodeNumber)
						return xref.StartEpisodeNumber - 1;
				}
			}

			return int.MaxValue;
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_MALUpdatedWatchedStatus_{0}", this.AnimeID);
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
				this.AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_MALUpdatedWatchedStatus", "AnimeID"));
				
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
