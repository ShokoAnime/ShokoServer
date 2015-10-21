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
using JMMModels;
using JMMModels.Childs;
using JMMServerModels.DB.Childs;
using JMMUser = JMMServer.Entities.JMMUser;
using VideoLocal = JMMServer.Entities.VideoLocal;

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
                JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId).GetUserWithAuth(AuthorizationProvider.AniDB);
                if (user == null)
                    return;
                AniDBAuthorization auth = user.GetAniDBAuthorization();

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
					    long size=0;
						JMMModels.AniDB_File anifile = Store.AniDB_FileRepo.FindByFileId(myitem.FileID);
						if (anifile != null)
                        { 
							hash = anifile.Hash;
                            size = anifile.FileSize;
                        }
						else
						{                            
							// look for manually linked files
						    AnimeSerie s=Store.AnimeSerieRepo.AnimeSerieFromAniDBEpisode(myitem.EpisodeID.ToString());
						    if (s != null)
						    {
						        JMMModels.Childs.AnimeEpisode aep = s.AnimeEpisodeFromAniDB_EpisodeId(myitem.EpisodeID.ToString());
						        List<JMMModels.VideoLocal> vls = aep.AniDbEpisodes.SelectMany(a => a.Value).SelectMany(a => a.VideoLocals).Where(a=>a.CrossRefSource!=CrossRefSourceType.AniDB).ToList();
						        if (vls.Count > 0)
                                { 
						            hash = vls[0].Hash;
                                    size = vls[0].FileSize;
                                }
                            }
                        }
						if (!string.IsNullOrEmpty(hash))
						{
							// find the video associated with this record
						    JMMModels.VideoLocal vl = Store.VideoLocalRepo.Find(hash, size);
                            if (vl == null) continue;
                            bool localStatus = false;
                            UserStats s = vl.UsersStats.FirstOrDefault(a => a.JMMUserId == user.Id);
						    if (s != null)
						        localStatus = s.WatchedCount > 0;
                            string action=string.Empty;
							if (localStatus != myitem.IsWatched)
							{
								if (localStatus)
								{
									// local = watched, anidb = unwatched
									if (ServerSettings.AniDB_MyList_ReadUnwatched)
									{
										modifiedItems++;
										vl.ToggleWatchedStatus(myitem.IsWatched, false, myitem.WatchedDate, user, false, true);
										action = "Used AniDB Status";
									}
								}
								else
								{
									// means local is un-watched, and anidb is watched
									if (ServerSettings.AniDB_MyList_ReadWatched)
									{
										modifiedItems++;
										vl.ToggleWatchedStatus(true, false, myitem.WatchedDate,user, false, true);
										action = "Updated Local record to Watched";
									}
								}

								string msg = $"MYLISTDIFF:: File {vl.FullServerPath()} - Local Status = {localStatus}, AniDB Status = {myitem.IsWatched} --- {action}";
								logger.Info(msg);
							}
						}

							

							//string msg = string.Format("MYLIST:: File {0} - Local Status = {1}, AniDB Status = {2} --- {3}",
							//	vl.FullServerPath, localStatus, myitem.IsWatched, action);
							//logger.Info(msg);

					}

					


					// now update all stats
					logger.Info("Process MyList: {0} Items, {1} Watched, {2} Modified", totalItems, watchedItems, modifiedItems);

					sched.LastUpdate = DateTime.Now;
                    Store.ScheduleUpdateRepo.Save(sched);
				}
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_SyncMyList: {0} ", ex.ToString());
				return;
			}
		}

	}
}
