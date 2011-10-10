using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;
using JMMServer.AniDB_API;
using JMMServer.WebCache;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_GetUpdated : CommandRequestImplementation, ICommandRequest
	{
		public bool ForceRefresh { get; set; }

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority4; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Getting list of updated animes from UDP API");
			}
		}

		public CommandRequest_GetUpdated()
		{
		}

		public CommandRequest_GetUpdated(bool forced)
		{
			this.ForceRefresh = forced;
			this.CommandType = (int)CommandRequestType.AniDB_GetUpdated;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_GetUpdated");

			try
			{
				List<int> animeIDsToUpdate = new List<int>();
				ScheduledUpdateRepository repSched = new ScheduledUpdateRepository();
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();

				//DateTime localTime = DateTime.Now.AddDays(-30);
				
				long startTime = 0;
				

				// check the automated update table to see when the last time we ran this command
				ScheduledUpdate sched = repSched.GetByUpdateType((int)ScheduledUpdateType.AniDBUpdates);
				if (sched != null)
				{
					int freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_Anime_UpdateFrequency);

					// if we have run this in the last 12 hours and are not forcing it, then exit
					TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
					if (tsLastRun.TotalHours < freqHours)
					{
						if (!ForceRefresh) return;
					}
				}

				// get a list of updates in the last day from AniDB
				// startTime will contain the date/time from which the updates apply to
				if (!JMMService.AnidbProcessor.GetUpdated(ref animeIDsToUpdate, ref startTime)) return;

				long webUpdateTime = 0;
				if (sched == null)
				{
					// if this is the first time, lets ask web cache for everyting in the last 3 days
					DateTime localTime = DateTime.Now.AddDays(-3);
					DateTime utcTime = localTime.ToUniversalTime();
					webUpdateTime = long.Parse(Utils.AniDBDate(utcTime));

					sched = new ScheduledUpdate();
					sched.UpdateType = (int)ScheduledUpdateType.AniDBUpdates;
				}
				else
				{
					logger.Trace("Last anidb info update was : {0}", sched.UpdateDetails);
					webUpdateTime = long.Parse(sched.UpdateDetails);
				}

				// now save the update time from AniDB
				// we will use this next time as a starting point when querying the web cache
				sched.LastUpdate = DateTime.Now;
				sched.UpdateDetails = startTime.ToString();
				repSched.Save(sched);

				// we now have a listof updates in the last 24 hours
				// get more from the web cache
				UpdatesCollection colUpdates = XMLService.Get_AniDBUpdates(webUpdateTime);
				// get a unqiue list of anime id's
				if (colUpdates != null)
				{
					logger.Info("Web cache updates : Time={0} - Count={1} - List={2}", webUpdateTime, colUpdates.UpdateCount, colUpdates.RawAnimeIDs);
					foreach (int id in colUpdates.AnimeIDs)
					{
						if (!animeIDsToUpdate.Contains(id)) animeIDsToUpdate.Add(id);
					}
				}
				else
				{
					logger.Info("No web Web cache updates");
				}

				int countAnime = 0;
				int countSeries = 0;
				foreach (int animeID in animeIDsToUpdate)
				{
					// update the anime from HTTP
					AniDB_Anime anime = repAnime.GetByAnimeID(animeID);
					if (anime == null)
					{
						logger.Trace("No local record found for Anime ID: {0}, so skipping...", animeID);
						continue;
					}

					logger.Info("Updating CommandRequest_GetUpdated: {0} ", animeID);

					// but only if it hasn't been recently updated
					TimeSpan ts = DateTime.Now - anime.DateTimeUpdated;
					if (ts.TotalHours > 4)
					{
						CommandRequest_GetAnimeHTTP cmdAnime = new CommandRequest_GetAnimeHTTP(animeID, true, false);
						cmdAnime.Save();
						countAnime++;
					}

					// update the group status
					// this will allow us to determine which anime has missing episodes
					// so we wonly get by an amime where we also have an associated series
					AnimeSeries ser = repSeries.GetByAnimeID(animeID);
					if (ser != null)
					{
						CommandRequest_GetReleaseGroupStatus cmdStatus = new CommandRequest_GetReleaseGroupStatus(animeID, true);
						cmdStatus.Save();
						countSeries++;
					}

				}

				logger.Info("Updating {0} anime records, and {1} group status records", countAnime, countSeries);

			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_GetUpdated: {0}", ex.ToString());
				return;
			}
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_GetUpdated");
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
				this.ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetUpdated", "ForceRefresh"));
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
