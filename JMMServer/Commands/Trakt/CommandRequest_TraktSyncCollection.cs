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
	public class CommandRequest_TraktSyncCollection : CommandRequestImplementation, ICommandRequest
	{
		public bool ForceRefresh { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority8; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Sync'ing Trakt Collection");
			}
		}

		public CommandRequest_TraktSyncCollection()
		{
		}

		public CommandRequest_TraktSyncCollection(bool forced)
		{
			this.CommandType = (int)CommandRequestType.Trakt_SyncCollection;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_TraktSyncCollection");

			try
			{
				ScheduledUpdateRepository repSched = new ScheduledUpdateRepository();
				ScheduledUpdate sched = repSched.GetByUpdateType((int)ScheduledUpdateType.TraktSync);
				if (sched == null)
				{
					sched = new ScheduledUpdate();
					sched.UpdateType = (int)ScheduledUpdateType.TraktSync;
					sched.UpdateDetails = "";
				}
				else
				{
					int freqHours = Utils.GetScheduledHours(ServerSettings.Trakt_SyncFrequency);

					// if we have run this in the last xxx hours then exit
					TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
					if (tsLastRun.TotalHours < freqHours)
					{
						if (!ForceRefresh) return;
					}
				}
				sched.LastUpdate = DateTime.Now;
				repSched.Save(sched);

				TraktTVHelper.SyncCollectionToTrakt();
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_TraktSyncCollection: {0}", ex.ToString());
				return;
			}
		}

		/// <summary>
		/// This should generate a unique key for a command
		/// It will be used to check whether the command has already been queued before adding it
		/// </summary>
		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_TraktSyncCollection");
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
				this.ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSyncCollection", "ForceRefresh"));
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
