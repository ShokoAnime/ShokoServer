using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;
using AniDBAPI.Commands;
using AniDBAPI;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_SyncMyList : CommandRequestImplementation, ICommandRequest
	{
		public bool ForceRefresh { get; set; }

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority6; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Syncing MyList info from HTTP API");
			}
		}

		public CommandRequest_SyncMyList()
		{
		}

		public CommandRequest_SyncMyList(bool forced)
		{
			this.ForceRefresh = forced;
			this.CommandType = (int)CommandRequestType.AniDB_SyncMyList;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_SyncMyList");

			try
			{
				// we will always assume that an anime was downloaded via http first
				ScheduledUpdateRepository repSched = new ScheduledUpdateRepository();
				AniDB_FileRepository repAniFile = new AniDB_FileRepository();
				VideoLocalRepository repVidLocals = new VideoLocalRepository();

				ScheduledUpdate sched = repSched.GetByUpdateType((int)ScheduledUpdateType.AniDBMyListSync);
				if (sched == null)
				{
					sched = new ScheduledUpdate();
					sched.UpdateType = (int)ScheduledUpdateType.AniDBMyListSync;
					sched.UpdateDetails = "";
				}
				else
				{
					int freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_MyList_UpdateFrequency);

					// if we have run this in the last 24 hours and are not forcing it, then exit
					TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
					if (tsLastRun.TotalHours < freqHours)
					{
						if (!ForceRefresh) return;
					}
				}

				AniDBHTTPCommand_GetMyList cmd = new AniDBHTTPCommand_GetMyList();
				cmd.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password);
				enHelperActivityType ev = cmd.Process();
				if (ev == enHelperActivityType.GotMyListHTTP)
				{
					int totalItems = 0;
					int watchedItems = 0;
					int modifiedItems = 0;
					double pct = 0;

					// 2. find files locally for the user, which are not recorded on anidb
					//    and then add them to anidb
					Dictionary<int, Raw_AniDB_MyListFile> onlineFiles = new Dictionary<int, Raw_AniDB_MyListFile>();
					foreach (Raw_AniDB_MyListFile myitem in cmd.MyListItems)
						onlineFiles[myitem.FileID] = myitem;

					Dictionary<string, AniDB_File> dictAniFiles = new Dictionary<string, AniDB_File>();
					List<AniDB_File> allAniFiles = repAniFile.GetAll();
					foreach (AniDB_File anifile in allAniFiles)
						dictAniFiles[anifile.Hash] = anifile;

					int missingFiles = 0;
					foreach (VideoLocal vid in repVidLocals.GetAll())
					{
						if (!dictAniFiles.ContainsKey(vid.Hash)) continue;

						int fileID = dictAniFiles[vid.Hash].FileID;

						if (!onlineFiles.ContainsKey(fileID))
						{
							// means we have found a file in our local collection, which is not recorded online
							CommandRequest_AddFileToMyList cmdAddFile = new CommandRequest_AddFileToMyList(vid.Hash);
							cmdAddFile.Save();
							missingFiles++;
						}
					}
					logger.Info(string.Format("MYLIST Missing Files: {0} Added to queue for inclusion", missingFiles));

					JMMUserRepository repUsers = new JMMUserRepository();
					List<JMMUser> aniDBUsers = repUsers.GetAniDBUsers();

					VideoLocal_UserRepository repVidUsers = new VideoLocal_UserRepository();

					// 1 . sync mylist items
					foreach (Raw_AniDB_MyListFile myitem in cmd.MyListItems)
					{
						totalItems++;
						if (myitem.IsWatched) watchedItems++;

						//calculate percentage
						pct = (double)totalItems / (double)cmd.MyListItems.Count * (double)100;
						string spct = pct.ToString("#0.0");

						AniDB_File anifile = repAniFile.GetByFileID(myitem.FileID);
						if (anifile != null)
						{
							// find the video associated with this record
							VideoLocal vl = repVidLocals.GetByHash(anifile.Hash);
							if (vl == null) continue;

							foreach (JMMUser juser in aniDBUsers)
							{
								bool localStatus = false;
								int? jmmUserID = null;

								// doesn't matter which anidb user we use
								jmmUserID = juser.JMMUserID;
								VideoLocal_User userRecord = vl.GetUserRecord(juser.JMMUserID);
								if (userRecord != null) localStatus = true;

								string action = "";
								if (localStatus != myitem.IsWatched)
								{
									if (localStatus == true)
									{
										// local = watched, anidb = unwatched
										if (ServerSettings.AniDB_MyList_ReadUnwatched)
										{
											modifiedItems++;
											if (jmmUserID.HasValue)
												vl.ToggleWatchedStatus(myitem.IsWatched, false, myitem.WatchedDate, false, jmmUserID.Value);
											action = "Used AniDB Status";
										}
									}
									else
									{
										// means local is un-watched, and anidb is watched
										if (ServerSettings.AniDB_MyList_ReadWatched)
										{
											modifiedItems++;
											if (jmmUserID.HasValue)
												vl.ToggleWatchedStatus(true, false, myitem.WatchedDate, false, jmmUserID.Value);
											action = "Updated Local record to Watched";
										}
									}

									string msg = string.Format("MYLISTDIFF:: File {0} - Local Status = {1}, AniDB Status = {2} --- {3}",
									vl.FullServerPath, localStatus, myitem.IsWatched, action);
									logger.Info(msg);
								}
							}

							

							//string msg = string.Format("MYLIST:: File {0} - Local Status = {1}, AniDB Status = {2} --- {3}",
							//	vl.FullServerPath, localStatus, myitem.IsWatched, action);
							//logger.Info(msg);
						}
					}

					


					// now update all stats
					Importer.UpdateAllStats();

					logger.Info("Process MyList: {0} Items, {1} Watched, {2} Modified", totalItems, watchedItems, modifiedItems);

					sched.LastUpdate = DateTime.Now;
					repSched.Save(sched);
				}
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_SyncMyList: {0} ", ex.ToString());
				return;
			}
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_SyncMyList");
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
				this.ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_SyncMyList", "ForceRefresh"));
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
