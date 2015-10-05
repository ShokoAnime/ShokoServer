using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;
using JMMDatabase;
using JMMModels;
using JMMModels.Childs;
using JMMModels.ClientExtensions;
using JMMServer.AniDB_API;
using JMMServer.WebCache;
using JMMServerModels.DB.Childs;
using AniDB_Anime = JMMServer.Entities.AniDB_Anime;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_GetUpdated : BaseCommandRequest, ICommandRequest
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
			this.CommandType = CommandRequestType.AniDB_GetUpdated;
			this.Priority = DefaultPriority;
		    this.Id = "CommandRequest_GetUpdated";
            this.JMMUserId = Store.JmmUserRepo.GetMasterUser().Id;
        }

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_GetUpdated");

			try
			{
				List<int> animeIDsToUpdate = new List<int>();
                JMMServerModels.DB.ScheduledUpdate sched=Store.ScheduleUpdateRepo.GetByUpdateType(ScheduledUpdateType.AniDBUpdates);
                if (sched!=null)
                { 
					int freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_Anime_UpdateFrequency);

					// if we have run this in the last 12 hours and are not forcing it, then exit
					TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
					if (tsLastRun.TotalHours < freqHours)
					{
						if (!ForceRefresh) return;
					}
				}

				

				long webUpdateTime = 0;
                long webUpdateTimeNew = 0;
				if (sched == null)
				{
					// if this is the first time, lets ask for last 3 days
					DateTime localTime = DateTime.Now.AddDays(-3);
					DateTime utcTime = localTime.ToUniversalTime();
				    webUpdateTime = utcTime.ToUnixTime();
				    webUpdateTimeNew = DateTime.Now.ToUniversalTime().ToUnixTime();
                    
					sched = new JMMServerModels.DB.ScheduledUpdate();
					sched.Type = ScheduledUpdateType.AniDBUpdates;
				}
				else
				{
					logger.Trace("Last anidb info update was : {0}", sched.Details);
					webUpdateTime = long.Parse(sched.Details);
                    webUpdateTimeNew = DateTime.Now.ToUniversalTime().ToUnixTime();

                    DateTime timeNow = DateTime.Now.ToUniversalTime();
                    logger.Info(string.Format("{0} since last UPDATED command", Utils.FormatSecondsToDisplayTime(int.Parse((webUpdateTimeNew -  webUpdateTime).ToString()))));
				}

                // get a list of updates from AniDB
                // startTime will contain the date/time from which the updates apply to
                JMMService.AnidbProcessor.GetUpdated(JMMUserId, ref animeIDsToUpdate, ref webUpdateTime);

				// now save the update time from AniDB
				// we will use this next time as a starting point when querying the web cache
				sched.LastUpdate = DateTime.Now;
                sched.Details = webUpdateTimeNew.ToString();
                Store.ScheduleUpdateRepo.Save(sched);
                if (animeIDsToUpdate.Count == 0)
                {
                    logger.Info("No anime to be updated");
                    return;
                }
                    

				int countAnime = 0;
				int countSeries = 0;
				foreach (int animeID in animeIDsToUpdate)
				{
					// update the anime from HTTP
				    AnimeSerie serie = Store.AnimeSerieRepo.AnimeSerieFromAniDBAnime(animeID.ToString());
					if (serie == null)
					{
						logger.Trace("No local record found for Anime ID: {0}, so skipping...", animeID);
						continue;
					}

					logger.Info("Updating CommandRequest_GetUpdated: {0} ", animeID);

					// but only if it hasn't been recently updated
				    TimeSpan ts = DateTime.Now - serie.AniDB_Anime.DateTimeUpdated;
                    if (ts.TotalHours > 4)
					{
						CommandRequest_GetAnimeHTTP cmdAnime = new CommandRequest_GetAnimeHTTP(animeID, true, false);
						cmdAnime.Save();
						countAnime++;
					}

					// update the group status
					// this will allow us to determine which anime has missing episodes
					// so we wonly get by an amime where we also have an associated series
					CommandRequest_GetReleaseGroupStatus cmdStatus = new CommandRequest_GetReleaseGroupStatus(animeID, true);
					cmdStatus.Save();
					countSeries++;
				}

				logger.Info("Updating {0} anime records, and {1} group status records", countAnime, countSeries);

			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_GetUpdated: {0}", ex.ToString());

			}
		}

	}
}
