using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using NLog;
using AniDBAPI;
using System.Threading;
using JMMServer.Entities;
using JMMServer.Repositories;
using System.IO;
using AniDBAPI.Commands;
using JMMServer.Commands;
using JMMServer.WebCache;
using JMMServer.Commands.Azure;
using NHibernate;

namespace JMMServer
{
	public class AniDBHelper
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		// we use this lock to make don't try and access AniDB too much (UDP and HTTP)
		private object lockAniDBConnections = new object();

		private IPEndPoint localIpEndPoint = null;
		private IPEndPoint remoteIpEndPoint = null;
		private Socket soUdp = null;
		private string curSessionID = string.Empty;
		
		private bool networkAvailable = true;

		private string userName = string.Empty;
		private string password = string.Empty;
		private string serverName = string.Empty;
		private string serverPort = string.Empty;
		private string clientPort = string.Empty;
		private Encoding encoding;

		System.Timers.Timer logoutTimer = null;

		public static int AniDBDelay = 2500;
		public static int AniDBDelay_Short = 1250;

		private DateTime? banTime = null;
		public DateTime? BanTime
		{
			get { return banTime; }
			set { banTime = value; }

		}

		private bool isBanned = false;
		public bool IsBanned
		{
			get { return isBanned; }
			
			set 
			{ 
				isBanned = value;
				BanTime = DateTime.Now;

				ServerInfo.Instance.IsBanned = isBanned;
				if (isBanned)
				{
					JMMService.CmdProcessorGeneral.Paused = true;
					ServerInfo.Instance.BanReason = BanTime.ToString();
				}
				else
					ServerInfo.Instance.BanReason = "";
			}

		}

		private bool isInvalidSession = false;
		public bool IsInvalidSession
		{
			get { return isInvalidSession; }

			set
			{
				isInvalidSession = value;
				ServerInfo.Instance.IsInvalidSession = isInvalidSession;
				
			}

		}

		private bool isLoggedOn = false;
		public bool IsLoggedOn
		{
			get { return isLoggedOn; }
			set { isLoggedOn = value; }

		}

		public AniDBHelper()
		{
		}

		public void Init(string userName, string password, string serverName, string serverPort, string clientPort)
		{
			soUdp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

			this.userName = userName;
			this.password = password;
			this.serverName = serverName;
			this.serverPort = serverPort;
			this.clientPort = clientPort;

			this.isLoggedOn = false;

			if (!BindToLocalPort()) networkAvailable = false;
			if (!BindToRemotePort()) networkAvailable = false;

			logoutTimer = new System.Timers.Timer();
			logoutTimer.Elapsed += new System.Timers.ElapsedEventHandler(logoutTimer_Elapsed);
			logoutTimer.Interval = 5000; // Set the Interval to 5 seconds.
			logoutTimer.Enabled = true;
			logoutTimer.AutoReset = true;

			logger.Info("starting logout timer...");
			logoutTimer.Start();
		}

		public void Dispose()
		{
			logger.Info("ANIDBLIB DISPOSING...");

			CloseConnections();
		}

		public void CloseConnections()
		{
			if (logoutTimer != null) logoutTimer.Stop();
			if (soUdp == null) return;

			soUdp.Shutdown(SocketShutdown.Both);
			soUdp.Close();
		}

		void logoutTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			if (!isLoggedOn) return;

			lock (lockAniDBConnections)
			{
				TimeSpan tsAniDBNonPing = DateTime.Now - JMMService.LastAniDBMessageNonPing;
				TimeSpan tsPing = DateTime.Now - JMMService.LastAniDBPing;
				TimeSpan tsAniDBUDP = DateTime.Now - JMMService.LastAniDBUDPMessage;

				// if we haven't sent a command for 20 seconds, send a ping just to keep the connection alive
				if (tsAniDBUDP.TotalSeconds >= 20 && tsPing.TotalSeconds >= 20 && !IsBanned)
				{
					AniDBCommand_Ping ping = new AniDBCommand_Ping();
					ping.Init();
					ping.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
				}

				string msg = string.Format("Last message sent {0} seconds ago", tsAniDBUDP.TotalSeconds);

				if (tsAniDBNonPing.TotalSeconds > 600) // after 10 minutes
				{
					ForceLogout();
				}
			}
		}

		private void Pause(AniDBPause pauseType)
		{
			int pauseDuration = AniDBDelay;
			if (pauseType == AniDBPause.Short) pauseDuration = AniDBDelay_Short;

			// do not send more than one message every 2 (2.4 to make sure) seconds
			while (DateTime.Now < JMMService.LastAniDBMessage.AddMilliseconds(pauseDuration))
			{
				// pretend to do something....
				Thread.Sleep(100);
			}
		}

		private void Pause()
		{
			Pause(AniDBPause.Long);
		}

		public bool Login()
		{
			// check if we are already logged in
			if (isLoggedOn) return true;

			if (!ValidAniDBCredentials()) return false;

			AniDBCommand_Login login = new AniDBCommand_Login();
			login.Init(userName, password);

			string msg = login.commandText.Replace(userName, "******");
			msg = msg.Replace(password, "******");
			logger.Trace("udp command: {0}", msg);
			enHelperActivityType ev = login.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));

			if (login.errorOccurred)
				logger.Trace("error in login: {0}", login.errorMessage);
			//else
			//	logger.Info("socketResponse: {0}", login.socketResponse);

			Thread.Sleep(2200);

			if (ev != enHelperActivityType.LoggedIn)
			{
				//BaseConfig.MyAnimeLog.Write("ProcessCommands: Login Failed!");
				//OnAniDBStatusEvent(new AniDBStatusEventArgs(enHelperActivityType.LoginFailed, ""));
				//aniDBCommands.Clear();
				//OnQueueUpdateEvent(new QueueUpdateEventArgs(this.QueueCount));
				// this will exit the thread
				return false;
			}
			else
			{
				curSessionID = login.SessionID;
				encoding = login.Encoding;
				this.isLoggedOn = true;
				this.IsInvalidSession = false;
				return true;
			}


		}

		public void ForceLogout()
		{
			if (isLoggedOn)
			{
				AniDBCommand_Logout logout = new AniDBCommand_Logout();
				logout.Init();
				//logger.Info("udp command: {0}", logout.commandText);
				logout.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
				//logger.Info("socketResponse: {0}", logout.socketResponse);
				isLoggedOn = false;
			}
		}

		public Raw_AniDB_File GetFileInfo(IHash vidLocal)
		{
			if (!Login()) return null;

			enHelperActivityType ev = enHelperActivityType.NoSuchFile;
			AniDBCommand_GetFileInfo getInfoCmd = null;

			lock (lockAniDBConnections)
			{
				Pause(AniDBPause.Short);

				getInfoCmd = new AniDBCommand_GetFileInfo();
				getInfoCmd.Init(vidLocal, true);
				ev = getInfoCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
			}

			if (ev == enHelperActivityType.GotFileInfo && getInfoCmd != null && getInfoCmd.fileInfo != null)
			{
				try
				{
					logger.Trace("ProcessResult_GetFileInfo: {0}", getInfoCmd.fileInfo.ToString());

					if (ServerSettings.AniDB_DownloadReleaseGroups)
					{
						CommandRequest_GetReleaseGroup cmdRelgrp = new CommandRequest_GetReleaseGroup(getInfoCmd.fileInfo.GroupID, false);
						cmdRelgrp.Save();
					}

					return getInfoCmd.fileInfo;
				}
				catch (Exception ex)
				{
					logger.Error(ex.ToString());
					return null;
				}
			}

			return null;
		}

		public void GetMyListFileStatus(int aniDBFileID)
		{
			if (!ServerSettings.AniDB_MyList_ReadWatched) return;

			if (!Login()) return;

			lock (lockAniDBConnections)
			{
				Pause();

				AniDBCommand_GetMyListFileInfo cmdGetFileStatus = new AniDBCommand_GetMyListFileInfo();
				cmdGetFileStatus.Init(aniDBFileID);
				enHelperActivityType ev = cmdGetFileStatus.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
				
			}
		}

		public void UpdateMyListStats()
		{
			if (!Login()) return;

			lock (lockAniDBConnections)
			{
				Pause();

				AniDBCommand_GetMyListStats cmdGetMylistStats = new AniDBCommand_GetMyListStats();
				cmdGetMylistStats.Init();
				enHelperActivityType ev = cmdGetMylistStats.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
				if (ev == enHelperActivityType.GotMyListStats && cmdGetMylistStats.MyListStats != null)
				{
					AniDB_MylistStatsRepository repStats = new AniDB_MylistStatsRepository();
					AniDB_MylistStats stat = null;
					List<AniDB_MylistStats> allStats = repStats.GetAll();
					if (allStats.Count == 0)
						stat = new AniDB_MylistStats();
					else
						stat = allStats[0];

					stat.Populate(cmdGetMylistStats.MyListStats);
					repStats.Save(stat);
				}
			}
		}

		public bool GetUpdated(ref List<int> updatedAnimeIDs, ref long startTime)
		{
			startTime = 0;
			updatedAnimeIDs = new List<int>();

			if (!Login()) return false;

			lock (lockAniDBConnections)
			{
				Pause();

				AniDBCommand_GetUpdated cmdUpdated = new AniDBCommand_GetUpdated();
				cmdUpdated.Init("1");
				enHelperActivityType ev = cmdUpdated.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));

				if (ev == enHelperActivityType.GotUpdated && cmdUpdated != null && cmdUpdated.RecordCount > 0)
				{
					startTime = long.Parse(cmdUpdated.StartTime);
					updatedAnimeIDs = cmdUpdated.AnimeIDList;

					// send the results to the web cache
					XMLService.Send_AniDBUpdates(cmdUpdated.StartTime, cmdUpdated.AnimeIDListRaw);

					return true;
				}
			}

			

			return false;

		}

		public void UpdateMyListFileStatus(IHash fileDataLocal, bool watched, DateTime? watchedDate)
		{
			if (!ServerSettings.AniDB_MyList_AddFiles) return;

			if (!Login()) return;

			lock (lockAniDBConnections)
			{
				Pause();

				AniDBCommand_UpdateFile cmdUpdateFile = new AniDBCommand_UpdateFile();
				cmdUpdateFile.Init(fileDataLocal, watched, watchedDate, true, null);
				enHelperActivityType ev = cmdUpdateFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
				if (ev == enHelperActivityType.NoSuchMyListFile && watched)
				{
					// the file is not actually on the user list, so let's add it
					// we do this by issueing the same command without the edit flag
					cmdUpdateFile = new AniDBCommand_UpdateFile();
					cmdUpdateFile.Init(fileDataLocal, watched, watchedDate, false, ServerSettings.AniDB_MyList_StorageState);
					ev = cmdUpdateFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
				}
			}
		}

		/// <summary>
		/// This is for generic files (manually linked)
		/// </summary>
		/// <param name="animeID"></param>
		/// <param name="episodeNumber"></param>
		/// <param name="watched"></param>
		public void UpdateMyListFileStatus(int animeID, int episodeNumber, bool watched)
		{
			if (!ServerSettings.AniDB_MyList_AddFiles) return;

			if (!Login()) return;

			lock (lockAniDBConnections)
			{
				Pause();

				AniDBCommand_UpdateFile cmdUpdateFile = new AniDBCommand_UpdateFile();
				cmdUpdateFile.Init(animeID, episodeNumber, watched, true);
				enHelperActivityType ev = cmdUpdateFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
				if (ev == enHelperActivityType.NoSuchMyListFile && watched)
				{
					// the file is not actually on the user list, so let's add it
					// we do this by issueing the same command without the edit flag
					cmdUpdateFile = new AniDBCommand_UpdateFile();
					cmdUpdateFile.Init(animeID, episodeNumber, watched, false);
					ev = cmdUpdateFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
				
				}
			}
		}

		public bool AddFileToMyList(IHash fileDataLocal, ref DateTime? watchedDate)
		{
			if (!ServerSettings.AniDB_MyList_AddFiles) return false;

			if (!Login()) return false;

			enHelperActivityType ev = enHelperActivityType.NoSuchMyListFile;
			AniDBCommand_AddFile cmdAddFile = null;

			lock (lockAniDBConnections)
			{
				Pause();

				cmdAddFile = new AniDBCommand_AddFile();
				cmdAddFile.Init(fileDataLocal, ServerSettings.AniDB_MyList_StorageState);
				ev = cmdAddFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
			}

			// if the user already has this file on 
			if (ev == enHelperActivityType.FileAlreadyExists && cmdAddFile.FileData != null)
			{
				watchedDate = cmdAddFile.WatchedDate;
				return cmdAddFile.ReturnIsWatched;

			}

			return false;
		}

		public bool AddFileToMyList(int animeID, int episodeNumber, ref DateTime? watchedDate)
		{
			if (!ServerSettings.AniDB_MyList_AddFiles) return false;

			if (!Login()) return false;

			enHelperActivityType ev = enHelperActivityType.NoSuchMyListFile;
			AniDBCommand_AddFile cmdAddFile = null;

			lock (lockAniDBConnections)
			{
				Pause();

				cmdAddFile = new AniDBCommand_AddFile();
				cmdAddFile.Init(animeID, episodeNumber, ServerSettings.AniDB_MyList_StorageState);
				ev = cmdAddFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
			}

			// if the user already has this file on 
			if (ev == enHelperActivityType.FileAlreadyExists && cmdAddFile.FileData != null)
			{
				watchedDate = cmdAddFile.WatchedDate;
				return cmdAddFile.ReturnIsWatched;

			}

			return false;
		}

		public bool DeleteFileFromMyList(string hash, long fileSize)
		{
			if (!ServerSettings.AniDB_MyList_AddFiles) return false;

			if (!Login()) return false;

			enHelperActivityType ev = enHelperActivityType.NoSuchMyListFile;
			AniDBCommand_DeleteFile cmdDelFile = null;

			lock (lockAniDBConnections)
			{
				Pause();

				cmdDelFile = new AniDBCommand_DeleteFile();
				cmdDelFile.Init(hash, fileSize);
				ev = cmdDelFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
			}

			return true;
		}

		public bool DeleteFileFromMyList(int fileID)
		{
			if (!ServerSettings.AniDB_MyList_AddFiles) return false;

			if (!Login()) return false;

			enHelperActivityType ev = enHelperActivityType.NoSuchMyListFile;
			AniDBCommand_DeleteFile cmdDelFile = null;

			lock (lockAniDBConnections)
			{
				Pause();

				cmdDelFile = new AniDBCommand_DeleteFile();
				cmdDelFile.Init(fileID);
				ev = cmdDelFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
			}

			return true;
		}

		public AniDB_Anime GetAnimeInfoUDP(int animeID, bool forceRefresh)
		{
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			AniDB_Anime anime = null;

			bool skip = true;
			if (forceRefresh)
				skip = false;
			else
			{
				anime = repAnime.GetByAnimeID(animeID);
				if (anime == null) skip = false;
			}

			if (skip)
			{
				if (anime == null)
					anime = repAnime.GetByAnimeID(animeID);

				return anime;

			}

			if (!Login()) return null;

			enHelperActivityType ev = enHelperActivityType.NoSuchAnime;
			AniDBCommand_GetAnimeInfo getAnimeCmd = null;

			lock (lockAniDBConnections)
			{
				Pause();

				getAnimeCmd = new AniDBCommand_GetAnimeInfo();
				getAnimeCmd.Init(animeID, forceRefresh);
				ev = getAnimeCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
			}

			if (ev == enHelperActivityType.GotAnimeInfo && getAnimeCmd.AnimeInfo != null)
			{
				// check for an existing record so we don't over write the description
				anime = repAnime.GetByAnimeID(getAnimeCmd.AnimeInfo.AnimeID);
				if (anime == null) anime = new AniDB_Anime();

				anime.PopulateAndSaveFromUDP(getAnimeCmd.AnimeInfo);
			}

			return anime;
		}

		public AniDB_Character GetCharacterInfoUDP(int charID)
		{
			if (!Login()) return null;

			enHelperActivityType ev = enHelperActivityType.NoSuchChar;
			AniDBCommand_GetCharacterInfo getCharCmd = null;
			lock (lockAniDBConnections)
			{
				Pause();

				getCharCmd = new AniDBCommand_GetCharacterInfo();
				getCharCmd.Init(charID, true);
				ev = getCharCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
			}

			AniDB_Character chr = null;
			if (ev == enHelperActivityType.GotCharInfo && getCharCmd.CharInfo != null)
			{
				AniDB_CharacterRepository repChar = new AniDB_CharacterRepository();
				chr = repChar.GetByCharID(charID);
				if (chr == null) chr = new AniDB_Character();

				chr.PopulateFromUDP(getCharCmd.CharInfo);
				repChar.Save(chr);
			}

			return chr;
		}

		// NO LONGER USED
		/*public AniDB_Seiyuu GetCreatorInfoUDP(int creatorID)
		{
			if (!Login()) return null;

			enHelperActivityType ev = enHelperActivityType.NoSuchCreator;
			AniDBCommand_GetCreatorInfo getCreatorCmd = null;
			lock (lockAniDBConnections)
			{
				Pause();

				getCreatorCmd = new AniDBCommand_GetCreatorInfo();
				getCreatorCmd.Init(creatorID, true);
				ev = getCreatorCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
			}

			AniDB_Seiyuu chr = null;
			if (ev == enHelperActivityType.GotCreatorInfo && getCreatorCmd.CreatorInfo != null)
			{
				AniDB_CreatorRepository repCreator = new AniDB_CreatorRepository();
				chr = repCreator.GetByCreatorID(creatorID);
				if (chr == null) chr = new AniDB_Seiyuu();

				chr.Populate(getCreatorCmd.CreatorInfo);
				repCreator.Save(chr);
			}

			return chr;
		}*/

		public AniDB_ReleaseGroup GetReleaseGroupUDP(int groupID)
		{
			if (!Login()) return null;

			enHelperActivityType ev = enHelperActivityType.NoSuchGroup;
			AniDBCommand_GetGroup getCmd = null;
			lock (lockAniDBConnections)
			{
				Pause();

				getCmd = new AniDBCommand_GetGroup();
				getCmd.Init(groupID);
				ev = getCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
			}

			AniDB_ReleaseGroupRepository repRelGrp = new AniDB_ReleaseGroupRepository();
			AniDB_ReleaseGroup relGroup = null;
			if (ev == enHelperActivityType.GotGroup && getCmd.Group != null)
			{
				relGroup = repRelGrp.GetByGroupID(groupID);
				if (relGroup == null) relGroup = new AniDB_ReleaseGroup();

				relGroup.Populate(getCmd.Group);
				repRelGrp.Save(relGroup);
			}

			return relGroup;
		}

		public GroupStatusCollection GetReleaseGroupStatusUDP(int animeID)
		{
			if (!Login()) return null;

			enHelperActivityType ev = enHelperActivityType.NoSuchCreator;
			AniDBCommand_GetGroupStatus getCmd = null;
			lock (lockAniDBConnections)
			{
				Pause();

				getCmd = new AniDBCommand_GetGroupStatus();
				getCmd.Init(animeID);
				ev = getCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
			}

			if (ev == enHelperActivityType.GotGroupStatus && getCmd.GrpStatusCollection != null)
			{
				// delete existing records
				AniDB_GroupStatusRepository repGrpStat = new AniDB_GroupStatusRepository();
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				AniDB_EpisodeRepository repAniEp = new AniDB_EpisodeRepository();
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();

				repGrpStat.DeleteForAnime(animeID);

				// save the records
				foreach (Raw_AniDB_GroupStatus raw in getCmd.GrpStatusCollection.Groups)
				{
					AniDB_GroupStatus grpstat = new AniDB_GroupStatus(raw);
					repGrpStat.Save(grpstat);
				}

				// updated cached stats
				// we don't do it in the save method as it would be too many unecessary updates
				logger.Trace("Updating group stats by anime from GetReleaseGroupStatusUDP: {0}", animeID);
				StatsCache.Instance.UpdateUsingAnime(animeID);
				
				if (getCmd.GrpStatusCollection.LatestEpisodeNumber > 0)
				{
					// update the anime with a record of the latest subbed episode
					AniDB_Anime anime = repAnime.GetByAnimeID(animeID);
					if (anime != null)
					{
						anime.LatestEpisodeNumber = getCmd.GrpStatusCollection.LatestEpisodeNumber;
						repAnime.Save(anime);

						// check if we have this episode in the database
						// if not get it now by updating the anime record
						List<AniDB_Episode> eps = repAniEp.GetByAnimeIDAndEpisodeNumber(animeID, getCmd.GrpStatusCollection.LatestEpisodeNumber);
						if (eps.Count == 0)
						{
							CommandRequest_GetAnimeHTTP cr_anime = new CommandRequest_GetAnimeHTTP(animeID, true, false);
							cr_anime.Save();
						}

						// update the missing episode stats on groups and children
						AnimeSeries series = repSeries.GetByAnimeID(animeID);
						if (series != null)
						{
							series.UpdateStats(true, true, true);
							//series.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
						}

					}
				}
			}

			return getCmd.GrpStatusCollection;
		}

		public CalendarCollection GetCalendarUDP()
		{
			if (!Login()) return null;

			enHelperActivityType ev = enHelperActivityType.CalendarEmpty;
			AniDBCommand_GetCalendar cmd = null;
			lock (lockAniDBConnections)
			{
				Pause();

				cmd = new AniDBCommand_GetCalendar();
				cmd.Init();
				ev = cmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
			}

			if (ev == enHelperActivityType.GotCalendar && cmd.Calendars != null)
				return cmd.Calendars;

			return null;
		}

		public AniDB_Review GetReviewUDP(int reviewID)
		{
			if (!Login()) return null;

			enHelperActivityType ev = enHelperActivityType.NoSuchReview;
			AniDBCommand_GetReview cmd = null;

			lock (lockAniDBConnections)
			{
				Pause();

				cmd = new AniDBCommand_GetReview();
				cmd.Init(reviewID);
				ev = cmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
			}

			AniDB_Review review = null;
			if (ev == enHelperActivityType.GotReview && cmd.ReviewInfo != null)
			{
				AniDB_ReviewRepository repReview = new AniDB_ReviewRepository();
				review = repReview.GetByReviewID(reviewID);
				if (review == null) review = new AniDB_Review();

				review.Populate(cmd.ReviewInfo);
				repReview.Save(review);
			}

			return review;
		}

		public bool VoteAnime(int animeID, decimal voteValue, enAniDBVoteType voteType)
		{
			if (!Login()) return false;

			enHelperActivityType ev = enHelperActivityType.NoSuchVote;
			AniDBCommand_Vote cmdVote = null;

			AniDB_VoteRepository repVotes = new AniDB_VoteRepository();

			lock (lockAniDBConnections)
			{
				Pause();

				cmdVote = new AniDBCommand_Vote();
				cmdVote.Init(animeID, voteValue, voteType);
				ev = cmdVote.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
				if (ev == enHelperActivityType.Voted || ev == enHelperActivityType.VoteUpdated)
				{
					List<AniDB_Vote> dbVotes = repVotes.GetByEntity(cmdVote.EntityID);
					AniDB_Vote thisVote = null;
					foreach (AniDB_Vote dbVote in dbVotes)
					{
						// we can only have anime permanent or anime temp but not both
						if (cmdVote.VoteType == enAniDBVoteType.Anime || cmdVote.VoteType == enAniDBVoteType.AnimeTemp)
						{
							if (dbVote.VoteType == (int)enAniDBVoteType.Anime || dbVote.VoteType == (int)enAniDBVoteType.AnimeTemp)
							{
								thisVote = dbVote;
							}
						}
						else
						{
							thisVote = dbVote;
						}
					}

					if (thisVote == null)
					{
						thisVote = new AniDB_Vote();
						thisVote.EntityID = cmdVote.EntityID;
					}
					thisVote.VoteType = (int)cmdVote.VoteType;
					thisVote.VoteValue = cmdVote.VoteValue;
					repVotes.Save(thisVote);
				}
			}

			return false;
		}

		public void VoteAnimeRevoke(int animeID, enAniDBVoteType voteType)
		{
			VoteAnime(animeID, -1, voteType);
		}


		public AniDB_Anime GetAnimeInfoHTTP(int animeID)
		{
			return GetAnimeInfoHTTP(animeID, false, true);
		}

		public AniDB_Anime GetAnimeInfoHTTP(int animeID, bool forceRefresh, bool downloadRelations)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetAnimeInfoHTTP(session, animeID, forceRefresh, downloadRelations);
			}
		}

		public AniDB_Anime GetAnimeInfoHTTP(ISession session, int animeID, bool forceRefresh, bool downloadRelations)
		{
			//if (!Login()) return null;

			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			AniDB_Anime anime = null;

			bool skip = true;
			if (forceRefresh)
				skip = false;
			else
			{
				anime = repAnime.GetByAnimeID(session, animeID);
				if (anime == null) skip = false;
			}

			if (skip)
			{
				if (anime == null)
					anime = repAnime.GetByAnimeID(session, animeID);

				return anime;

			}

			AniDBHTTPCommand_GetFullAnime getAnimeCmd = null;
			lock (lockAniDBConnections)
			{
				Pause();

				getAnimeCmd = new AniDBHTTPCommand_GetFullAnime();
				getAnimeCmd.Init(animeID, false);
				getAnimeCmd.Process();
			}

			if (getAnimeCmd.Anime != null)
			{
				//XMLService.Send_AniDB_Anime_Full(getAnimeCmd.AnimeID, getAnimeCmd.XmlResult);

				logger.Trace("cmdResult.Anime: {0}", getAnimeCmd.Anime);

				anime = repAnime.GetByAnimeID(session, animeID);
				if (anime == null)
					anime = new AniDB_Anime();
				anime.PopulateAndSaveFromHTTP(session, getAnimeCmd.Anime, getAnimeCmd.Episodes, getAnimeCmd.Titles, getAnimeCmd.Categories, getAnimeCmd.Tags,
					getAnimeCmd.Characters, getAnimeCmd.Relations, getAnimeCmd.SimilarAnime, getAnimeCmd.Recommendations, downloadRelations);

				// Request an image download
				CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(anime.AniDB_AnimeID, JMMImageType.AniDB_Cover, false);
				cmd.Save(session);
				// create AnimeEpisode records for all episodes in this anime
				// only if we have a series
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				AnimeSeries ser = repSeries.GetByAnimeID(session, animeID);
				if (ser != null)
				{
					ser.CreateAnimeEpisodes(session);
				}

				// update cached stats
				StatsCache.Instance.UpdateUsingAnime(session, anime.AnimeID);
				StatsCache.Instance.UpdateAnimeContract(session, anime.AnimeID);

				// download character images
				foreach (AniDB_Anime_Character animeChar in anime.GetAnimeCharacters(session))
				{
					AniDB_Character chr = animeChar.GetCharacter(session);
					if (chr == null) continue;

					if (ServerSettings.AniDB_DownloadCharacters)
					{
						if (!string.IsNullOrEmpty(chr.PosterPath) && !File.Exists(chr.PosterPath))
						{
							logger.Debug("Downloading character image: {0} - {1}({2}) - {3}", anime.MainTitle, chr.CharName, chr.CharID, chr.PosterPath);
							cmd = new CommandRequest_DownloadImage(chr.AniDB_CharacterID, JMMImageType.AniDB_Character, false);
							cmd.Save();
						}
					}

					if (ServerSettings.AniDB_DownloadCreators)
					{
						AniDB_Seiyuu seiyuu = chr.GetSeiyuu(session);
						if (seiyuu == null || string.IsNullOrEmpty(seiyuu.PosterPath)) continue;

						if (!File.Exists(seiyuu.PosterPath))
						{
							logger.Debug("Downloading seiyuu image: {0} - {1}({2}) - {3}", anime.MainTitle, seiyuu.SeiyuuName, seiyuu.SeiyuuID, seiyuu.PosterPath);
							cmd = new CommandRequest_DownloadImage(seiyuu.AniDB_SeiyuuID, JMMImageType.AniDB_Creator, false);
							cmd.Save();
						}
					}

				}
				
				//OnGotAnimeInfoEvent(new GotAnimeInfoEventArgs(getAnimeCmd.Anime.AnimeID));
				CommandRequest_Azure_SendAnimeFull cmdAzure = new CommandRequest_Azure_SendAnimeFull(anime.AnimeID);
				cmdAzure.Save(session);
				
			}


			return anime;
		}

		public bool ValidAniDBCredentials()
		{
			if (string.IsNullOrEmpty(this.userName) || string.IsNullOrEmpty(this.password) || string.IsNullOrEmpty(this.serverName)
				|| string.IsNullOrEmpty(this.serverPort) || string.IsNullOrEmpty(this.clientPort))
			{
				//OnAniDBStatusEvent(new AniDBStatusEventArgs(enHelperActivityType.OtherError, "ERROR: Please enter valid AniDB credentials via Configuration first"));
				return false;
			}

			return true;
		}

		private bool BindToLocalPort()
		{
			// only do once
			//if (localIpEndPoint != null) return false;
			localIpEndPoint = null;

			// Dont send Expect 100 requests. These requests arnt always supported by remote internet devices, in which case can cause failure.
			System.Net.ServicePointManager.Expect100Continue = false;

			IPHostEntry localHostEntry;
			localHostEntry = Dns.GetHostEntry(Dns.GetHostName());


			logger.Info("-------- Local IP Addresses --------");
			localIpEndPoint = new IPEndPoint(IPAddress.Any, Convert.ToInt32(clientPort));
			logger.Info("-------- End Local IP Addresses --------");

			soUdp.Bind(localIpEndPoint);
			soUdp.ReceiveTimeout = 30000; // 30 seconds

			logger.Info("BindToLocalPort: Bound to local address: {0} - Port: {1} ({2})", localIpEndPoint.ToString(), clientPort, localIpEndPoint.AddressFamily);

			return true;
		}

		private bool BindToRemotePort()
		{
			// only do once
			remoteIpEndPoint = null;
			//if (remoteIpEndPoint != null) return true;

			try
			{
				IPHostEntry remoteHostEntry = Dns.GetHostEntry(serverName);
				remoteIpEndPoint = new IPEndPoint(remoteHostEntry.AddressList[0], Convert.ToInt32(serverPort));

				logger.Info("BindToRemotePort: Bound to remote address: " + remoteIpEndPoint.Address.ToString() + " : " +
					remoteIpEndPoint.Port.ToString());

				return true;
			}
			catch (Exception ex)
			{
				logger.ErrorException(string.Format("Could not bind to remote port: {0}", ex.ToString()), ex);
				return false;
			}


		}
	}
}
