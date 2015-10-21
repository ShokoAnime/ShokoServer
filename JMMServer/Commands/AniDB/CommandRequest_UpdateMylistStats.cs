using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;
using AniDBAPI;
using JMMDatabase;
using JMMDatabase.Extensions;
using JMMModels.Childs;
using JMMServerModels.DB.Childs;

namespace JMMServer.Commands.AniDB
{
	[Serializable]
	public class CommandRequest_UpdateMylistStats : BaseCommandRequest, ICommandRequest
	{
		public bool ForceRefresh { get; set; }

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority10; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Updating AniDB MyListStats");
			}
		}

		public CommandRequest_UpdateMylistStats()
		{
		}

		public CommandRequest_UpdateMylistStats(string userid, bool forced)
		{
			this.ForceRefresh = forced;
			this.CommandType = CommandRequestType.AniDB_UpdateMylistStats;
			this.Priority = DefaultPriority;
		    this.JMMUserId = userid;
			this.Id= "CommandRequest_UpdateMylistStats";
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_UpdateMylistStats");

			try
			{
                JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId).GetUserWithAuth(AuthorizationProvider.AniDB);
                if (user == null)
                    return;

                // we will always assume that an anime was downloaded via http first
                ScheduledUpdateRepository repSched = new ScheduledUpdateRepository();
				JMMServerModels.DB.ScheduledUpdate sched = Store.ScheduleUpdateRepo.GetByUpdateType(ScheduledUpdateType.AniDBMylistStats);
				if (sched == null)
				{
					sched = new JMMServerModels.DB.ScheduledUpdate();
					sched.Type = ScheduledUpdateType.AniDBMylistStats;
					sched.Details = "";
				}
				else
				{
					int freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_MyListStats_UpdateFrequency);

					// if we have run this in the last 24 hours and are not forcing it, then exit
					TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
					if (tsLastRun.TotalHours < freqHours)
					{
						if (!ForceRefresh) return;
					}
				}

				sched.LastUpdate = DateTime.Now;
                Store.ScheduleUpdateRepo.Save(sched);

				JMMService.AnidbProcessor.UpdateMyListStats(user.Id);


			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_UpdateMylistStats: {0}", ex.ToString());
				return;
			}
		}

	}
}
