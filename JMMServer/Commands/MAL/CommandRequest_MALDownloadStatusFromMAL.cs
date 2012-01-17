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
	public class CommandRequest_MALDownloadStatusFromMAL : CommandRequestImplementation, ICommandRequest
	{
		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Downloading watched states from MAL");
			}
		}


		public CommandRequest_MALDownloadStatusFromMAL()
		{
			this.CommandType = (int)CommandRequestType.MAL_DownloadWatchedStates;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_MALDownloadStatusFromMAL");

			try
			{
				// find the latest eps to update
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();

				myanimelist mal = MALHelper.GetMALAnimeList();
				if (mal == null) return;
				if (mal.anime == null) return;

				CrossRef_AniDB_MALRepository repCrossRef = new CrossRef_AniDB_MALRepository();

				foreach (myanimelistAnime malAnime in mal.anime)
				{
					// look up the anime
					CrossRef_AniDB_MAL xref = repCrossRef.GetByMALID(malAnime.series_animedb_id);
					if (xref == null) continue;


				}
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_MALDownloadStatusFromMAL: {0}", ex.ToString());
				return;
			}
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_MALDownloadStatusFromMAL");
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
