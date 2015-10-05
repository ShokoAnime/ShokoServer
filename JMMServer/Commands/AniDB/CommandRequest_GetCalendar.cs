using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;
using AniDBAPI.Commands;
using AniDBAPI;
using JMMDatabase;
using JMMModels.Childs;
using JMMModels.ClientExtensions;
using JMMServerModels.DB.Childs;
using Raven.Client;
using AniDB_Anime = JMMServer.Entities.AniDB_Anime;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_GetCalendar : BaseCommandRequest, ICommandRequest
	{
		public bool ForceRefresh { get; set; }

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority7; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Getting calendar info from UDP API");
			}
		}

		public CommandRequest_GetCalendar()
		{
		}

		public CommandRequest_GetCalendar(bool forced)
		{
			this.ForceRefresh = forced;
			this.CommandType = CommandRequestType.AniDB_GetCalendar;
			this.Priority = DefaultPriority;
            this.JMMUserId = Store.JmmUserRepo.GetMasterUser().Id;
            this.Id = "CommandRequest_GetCalendar";
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_GetCalendar");

			try
			{
			    IDocumentSession session = Store.GetSession();
				// we will always assume that an anime was downloaded via http first
				JMMServerModels.DB.ScheduledUpdate sched = Store.ScheduleUpdateRepo.GetByUpdateType(ScheduledUpdateType.AniDBCalendar);
				if (sched == null)
				{
					sched = new JMMServerModels.DB.ScheduledUpdate();
					sched.Type = ScheduledUpdateType.AniDBCalendar;
					sched.Details = "";
				}
				else
				{
					int freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_Calendar_UpdateFrequency);

					// if we have run this in the last 12 hours and are not forcing it, then exit
					TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
					if (tsLastRun.TotalHours < freqHours)
					{
						if (!ForceRefresh) return;
					}
				}

				sched.LastUpdate = DateTime.Now;
                Store.ScheduleUpdateRepo.Save(sched,session);

				CalendarCollection colCalendars = JMMService.AnidbProcessor.GetCalendarUDP(JMMUserId);
				if (colCalendars == null || colCalendars.Calendars == null)
				{
					logger.Error("Could not get calendar from AniDB");
					return;
				}
				foreach (Calendar cal in colCalendars.Calendars)
				{
				    JMMModels.AnimeSerie serie = Store.AnimeSerieRepo.AnimeSerieFromAniDBAnime(cal.AnimeID.ToString());
					if (serie != null)
					{
						// don't update if the local data is less 2 days old
						TimeSpan ts = DateTime.Now - serie.AniDB_Anime.DateTimeUpdated;
						if (ts.TotalDays >= 2)
						{
							CommandRequest_GetAnimeHTTP cmdAnime = new CommandRequest_GetAnimeHTTP(cal.AnimeID, true, false);
							cmdAnime.Save();
						}
						else
						{
							// update the release date even if we don't update the anime record
						    if (!cal.ReleaseDate.HasValue)
						        serie.AniDB_Anime.AirDate = null;
                            else
                                serie.AniDB_Anime.AirDate = new AniDB_Date { Precision = AniDB_Date_Precision.Day, Date=cal.ReleaseDate.Value.ToUnixTime()};
                            Store.AnimeSerieRepo.Save(serie);

						}
					}
					else
					{
						CommandRequest_GetAnimeHTTP cmdAnime = new CommandRequest_GetAnimeHTTP(cal.AnimeID, true, false);
						cmdAnime.Save();
					}
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error processing CommandRequest_GetCalendar: " + ex.ToString(), ex);
				return;
			}
		}

	}
}
