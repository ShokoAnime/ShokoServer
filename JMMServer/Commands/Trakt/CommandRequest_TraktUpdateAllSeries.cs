using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using System.Xml;
using JMMServer.Repositories;
using JMMServer.Providers.TraktTV;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_TraktUpdateAllSeries : BaseCommandRequest, ICommandRequest
	{
		public bool ForceRefresh { get; set; }


		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Updating all Trakt Series Info");
			}
		}

		public CommandRequest_TraktUpdateAllSeries()
		{
		}

		public CommandRequest_TraktUpdateAllSeries(bool forced)
		{
			this.CommandType = (int)CommandRequestType.Trakt_UpdateAllSeries;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_TraktUpdateAllSeries");

			try
			{
				ScheduledUpdateRepository repSched = new ScheduledUpdateRepository();
				ScheduledUpdate sched = repSched.GetByUpdateType((int)ScheduledUpdateType.TraktUpdate);
				if (sched == null)
				{
					sched = new ScheduledUpdate();
					sched.UpdateType = (int)ScheduledUpdateType.TraktUpdate;
					sched.UpdateDetails = "";
				}
				else
				{
					int freqHours = Utils.GetScheduledHours(ServerSettings.Trakt_UpdateFrequency);

					// if we have run this in the last xxx hours then exit
					TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
					if (tsLastRun.TotalHours < freqHours)
					{
						if (!ForceRefresh) return;
					}
				}
				sched.LastUpdate = DateTime.Now;
				repSched.Save(sched);

				// update all info
				TraktTVHelper.UpdateAllInfo();

				// scan for new matches
				TraktTVHelper.ScanForMatches();
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_TraktUpdateAllSeries: {0}", ex.ToString());
				return;
			}
		}

		/// <summary>
		/// This should generate a unique key for a command
		/// It will be used to check whether the command has already been queued before adding it
		/// </summary>
		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_TraktUpdateAllSeries");
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
				this.ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_TraktUpdateAllSeries", "ForceRefresh"));
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
