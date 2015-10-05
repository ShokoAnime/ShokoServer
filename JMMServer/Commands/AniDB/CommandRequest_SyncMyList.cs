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
using JMMDatabase.Extensions;
using JMMModels.Childs;
using JMMServerModels.DB.Childs;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_SyncMyList : BaseCommandRequest, ICommandRequest
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

		public CommandRequest_SyncMyList(string userid, bool forced)
		{
			this.ForceRefresh = forced;
		    this.JMMUserId = userid;
			this.CommandType = CommandRequestType.AniDB_SyncMyList;
			this.Priority = DefaultPriority;
		    this.JMMUserId = userid;
            this.Id= "CommandRequest_SyncMyList";
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

				JMMServerModels.DB.ScheduledUpdate sched = Store.ScheduleUpdateRepo.GetByUpdateType(ScheduledUpdateType.AniDBMyListSync);
				if (sched == null)
				{
					sched = new JMMServerModels.DB.ScheduledUpdate();
					sched.Type = ScheduledUpdateType.AniDBMyListSync;
					sched.Details = "";
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
			    JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId).GetRealUser();
			    AniDBAuthorization auth = user.GetAniDBAuthorizationFromUser();

				cmd.Init(auth.UserName, auth.Password);
                enHelperActivityType ev = cmd.Process();
				if (ev == enHelperActivityType.GotMyListHTTP && cmd.MyListItems.Count > 1)
				{
					int totalItems = 0;
					int watchedItems = 0;
					int modifiedItems = 0;
					double pct = 0;

					// 2. find files locally for the user, which are not recorded on anidb
					//    and then add them to anidb
				    Dictionary<int, Raw_AniDB_MyListFile> onlineFiles = cmd.MyListItems.ToDictionary(a => a.FileID, a => a);
                    int missingFiles = 0;

				    foreach (string s in Store.VideoLocalRepo.GetVideolocalsWithoutAniDBFile())
				    {
				        int fileID = Store.AniDB_FileRepo.Find(s).FileId;
                        if (!onlineFiles.ContainsKey(fileID))
                        {
                            // means we have found a file in our local collection, which is not recorded online
                            CommandRequest_AddFileToMyList cmdAddFile = new CommandRequest_AddFileToMyList(JMMUserId, s);
                            cmdAddFile.Save();
                            missingFiles++;
                        }
                    }
					logger.Info($"MYLIST Missing Files: {missingFiles} Added to queue for inclusion");

					JMMUserRepository repUsers = new JMMUserRepository();
					List<JMMUser> aniDBUsers = repUsers.GetAniDBUsers();

					VideoLocal_UserRepository repVidUsers = new VideoLocal_UserRepository();
					CrossRef_File_EpisodeRepository repFileEp = new CrossRef_File_EpisodeRepository();

					// 1 . sync mylist items
					foreach (Raw_AniDB_MyListFile myitem in cmd.MyListItems)
					{
						// ignore files mark as deleted by the user
						if (myitem.State == (int)AniDBFileStatus.Deleted) continue;

						totalItems++;
						if (myitem.IsWatched) watchedItems++;

						//calculate percentage
						pct = (double)totalItems / (double)cmd.MyListItems.Count * (double)100;
						string spct = pct.ToString("#0.0");

						string hash = string.Empty;

						AniDB_File anifile = repAniFile.GetByFileID(myitem.FileID);
						if (anifile != null)
							hash = anifile.Hash;
						else
						{
							// look for manually linked files
							List<CrossRef_File_Episode> xrefs = repFileEp.GetByEpisodeID(myitem.EpisodeID);
							foreach (CrossRef_File_Episode xref in xrefs)
							{
								if (xref.CrossRefSource != (int)CrossRefSource.AniDB)
								{
									hash = xref.Hash;
									break;
								}
							}
						}


						if (!string.IsNullOrEmpty(hash))
						{
							// find the video associated with this record
							VideoLocal vl = repVidLocals.GetByHash(hash);
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
												vl.ToggleWatchedStatus(myitem.IsWatched, false, myitem.WatchedDate, false, false, jmmUserID.Value, false, true);
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
												vl.ToggleWatchedStatus(true, false, myitem.WatchedDate, false, false, jmmUserID.Value, false, true);
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
