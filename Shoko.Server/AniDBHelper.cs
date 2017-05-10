using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using AniDBAPI;
using AniDBAPI.Commands;
using NHibernate;
using NLog;
using System.Windows;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server
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
                    ShokoService.CmdProcessorGeneral.Paused = true;
                    ServerInfo.Instance.BanReason = BanTime.ToString();
                }
                else
                    ServerInfo.Instance.BanReason = "";
            }
        }

        private string banOrigin = "";

        public string BanOrigin
        {
            get { return banOrigin; }
            set
            {
                banOrigin = value;
                ServerInfo.Instance.BanOrigin = value;
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

        private bool waitingOnResponse = false;

        public bool WaitingOnResponse
        {
            get { return waitingOnResponse; }
            set { waitingOnResponse = value; }
        }

        private DateTime? waitingOnResponseTime = null;

        public DateTime? WaitingOnResponseTime
        {
            get { return waitingOnResponseTime; }
            set { waitingOnResponseTime = value; }
        }

        private int? extendPauseSecs = null;

        public int? ExtendPauseSecs
        {
            get { return extendPauseSecs; }
            set { extendPauseSecs = value; }
        }

        private string extendPauseReason = "";

        public string ExtendPauseReason
        {
            get { return extendPauseReason; }
            set { extendPauseReason = value; }
        }

        public static event EventHandler LoginFailed;

        public AniDBHelper()
        {
        }

        public void ExtendPause(int secsToPause, string pauseReason)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            ExtendPauseSecs = secsToPause;
            ExtendPauseReason = pauseReason;
            ServerInfo.Instance.ExtendedPauseString = string.Format(Shoko.Commons.Properties.Resources.AniDB_Paused,
                secsToPause,
                pauseReason);
            ServerInfo.Instance.HasExtendedPause = true;
        }

        public void ResetExtendPause()
        {
            ExtendPauseSecs = null;
            ExtendPauseReason = "";
            ServerInfo.Instance.ExtendedPauseString = "";
            ServerInfo.Instance.HasExtendedPause = false;
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
            logoutTimer?.Stop();
            logoutTimer = null;
            if (soUdp == null) return;
            if (soUdp.Connected) soUdp.Shutdown(SocketShutdown.Both);
            soUdp.Close();
            soUdp = null;
        }

        void logoutTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            TimeSpan tsAniDBUDPTemp = DateTime.Now - ShokoService.LastAniDBUDPMessage;
            if (ExtendPauseSecs.HasValue && tsAniDBUDPTemp.TotalSeconds >= ExtendPauseSecs.Value)
                ResetExtendPause();

            if (!isLoggedOn) return;

            // don't ping when anidb is taking a long time to respond
            if (WaitingOnResponse)
            {
                try
                {
                    if (WaitingOnResponseTime.HasValue)
                    {
                        Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                        TimeSpan ts = DateTime.Now - WaitingOnResponseTime.Value;
                        ServerInfo.Instance.WaitingOnResponseAniDBUDPString =
                            string.Format(Shoko.Commons.Properties.Resources.AniDB_ResponseWaitSeconds,
                                ts.TotalSeconds);
                    }
                }
                catch
                {
                }
                return;
            }

            lock (lockAniDBConnections)
            {
                TimeSpan tsAniDBNonPing = DateTime.Now - ShokoService.LastAniDBMessageNonPing;
                TimeSpan tsPing = DateTime.Now - ShokoService.LastAniDBPing;
                TimeSpan tsAniDBUDP = DateTime.Now - ShokoService.LastAniDBUDPMessage;

                // if we haven't sent a command for 45 seconds, send a ping just to keep the connection alive
                if (tsAniDBUDP.TotalSeconds >= Constants.PingFrequency &&
                    tsPing.TotalSeconds >= Constants.PingFrequency &&
                    !IsBanned && !ExtendPauseSecs.HasValue)
                {
                    AniDBCommand_Ping ping = new AniDBCommand_Ping();
                    ping.Init();
                    ping.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                }

                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                string msg = string.Format(Shoko.Commons.Properties.Resources.AniDB_LastMessage,
                    tsAniDBUDP.TotalSeconds);

                if (tsAniDBNonPing.TotalSeconds > Constants.ForceLogoutPeriod) // after 10 minutes
                {
                    ForceLogout();
                }
            }
        }

        private void SetWaitingOnResponse(bool isWaiting)
        {
            WaitingOnResponse = isWaiting;
            ServerInfo.Instance.WaitingOnResponseAniDBUDP = isWaiting;

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            if (isWaiting)
                ServerInfo.Instance.WaitingOnResponseAniDBUDPString =
                    Shoko.Commons.Properties.Resources.AniDB_ResponseWait;
            else
                ServerInfo.Instance.WaitingOnResponseAniDBUDPString = Shoko.Commons.Properties.Resources.Command_Idle;

            if (isWaiting)
                WaitingOnResponseTime = DateTime.Now;
            else
                WaitingOnResponseTime = null;
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
            SetWaitingOnResponse(true);
            enHelperActivityType ev = login.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                new UnicodeEncoding(true, false));
            SetWaitingOnResponse(false);

            if (login.errorOccurred)
                logger.Trace("error in login: {0}", login.errorMessage);
            //else
            //	logger.Info("socketResponse: {0}", login.socketResponse);

            Thread.Sleep(2200);

            if (ev != enHelperActivityType.LoggedIn)
            {
                LoginFailed?.Invoke(this, null);

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
                SetWaitingOnResponse(true);
                logout.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
                //logger.Info("socketResponse: {0}", logout.socketResponse);
                isLoggedOn = false;
            }
        }

        public Raw_AniDB_Episode GetEpisodeInfo(int episodeID)
        {
            if (!Login()) return null;

            enHelperActivityType ev = enHelperActivityType.NoSuchEpisode;
            AniDBCommand_GetEpisodeInfo getInfoCmd = null;

            lock (lockAniDBConnections)
            {
                getInfoCmd = new AniDBCommand_GetEpisodeInfo();
                getInfoCmd.Init(episodeID, true);
                SetWaitingOnResponse(true);
                ev = getInfoCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            if (ev == enHelperActivityType.GotEpisodeInfo && getInfoCmd != null && getInfoCmd.EpisodeInfo != null)
            {
                try
                {
                    logger.Trace("ProcessResult_GetEpisodeInfo: {0}", getInfoCmd.EpisodeInfo.ToString());
                    return getInfoCmd.EpisodeInfo;
                }
                catch (Exception ex)
                {
                    logger.Error(ex.ToString());
                    return null;
                }
            }

            return null;
        }

        public Raw_AniDB_File GetFileInfo(IHash vidLocal)
        {
            if (!Login()) return null;

            enHelperActivityType ev = enHelperActivityType.NoSuchFile;
            AniDBCommand_GetFileInfo getInfoCmd = null;

            lock (lockAniDBConnections)
            {
                getInfoCmd = new AniDBCommand_GetFileInfo();
                getInfoCmd.Init(vidLocal, true);
                SetWaitingOnResponse(true);
                ev = getInfoCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            if (ev == enHelperActivityType.GotFileInfo && getInfoCmd != null && getInfoCmd.fileInfo != null)
            {
                try
                {
                    logger.Trace("ProcessResult_GetFileInfo: {0}", getInfoCmd.fileInfo.ToString());

                    if (ServerSettings.AniDB_DownloadReleaseGroups)
                    {
                        CommandRequest_GetReleaseGroup cmdRelgrp =
                            new CommandRequest_GetReleaseGroup(getInfoCmd.fileInfo.GroupID, false);
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
                AniDBCommand_GetMyListFileInfo cmdGetFileStatus = new AniDBCommand_GetMyListFileInfo();
                cmdGetFileStatus.Init(aniDBFileID);
                SetWaitingOnResponse(true);
                enHelperActivityType ev = cmdGetFileStatus.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }
        }

        public void UpdateMyListStats()
        {
            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                AniDBCommand_GetMyListStats cmdGetMylistStats = new AniDBCommand_GetMyListStats();
                cmdGetMylistStats.Init();
                SetWaitingOnResponse(true);
                enHelperActivityType ev = cmdGetMylistStats.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
                if (ev == enHelperActivityType.GotMyListStats && cmdGetMylistStats.MyListStats != null)
                {
                    AniDB_MylistStats stat = null;
                    IReadOnlyList<AniDB_MylistStats> allStats = RepoFactory.AniDB_MylistStats.GetAll();
                    if (allStats.Count == 0)
                        stat = new AniDB_MylistStats();
                    else
                        stat = allStats[0];

                    stat.Populate(cmdGetMylistStats.MyListStats);
                    RepoFactory.AniDB_MylistStats.Save(stat);
                }
            }
        }

        public bool GetUpdated(ref List<int> updatedAnimeIDs, ref long startTime)
        {
            //startTime = 0;
            updatedAnimeIDs = new List<int>();

            if (!Login()) return false;

            lock (lockAniDBConnections)
            {
                AniDBCommand_GetUpdated cmdUpdated = new AniDBCommand_GetUpdated();
                cmdUpdated.Init(startTime.ToString());
                SetWaitingOnResponse(true);
                enHelperActivityType ev = cmdUpdated.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);

                if (ev == enHelperActivityType.GotUpdated && cmdUpdated != null && cmdUpdated.RecordCount > 0)
                {
                    startTime = long.Parse(cmdUpdated.StartTime);
                    updatedAnimeIDs = cmdUpdated.AnimeIDList;

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
                AniDBCommand_UpdateFile cmdUpdateFile = new AniDBCommand_UpdateFile();
                cmdUpdateFile.Init(fileDataLocal, watched, watchedDate, true, null);
                SetWaitingOnResponse(true);
                enHelperActivityType ev = cmdUpdateFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
                if (ev == enHelperActivityType.NoSuchMyListFile && watched)
                {
                    // the file is not actually on the user list, so let's add it
                    // we do this by issueing the same command without the edit flag
                    cmdUpdateFile = new AniDBCommand_UpdateFile();
                    cmdUpdateFile.Init(fileDataLocal, watched, watchedDate, false,
                        ServerSettings.AniDB_MyList_StorageState);
                    ev = cmdUpdateFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                        new UnicodeEncoding(true, false));
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
                AniDBCommand_UpdateFile cmdUpdateFile = new AniDBCommand_UpdateFile();
                cmdUpdateFile.Init(animeID, episodeNumber, watched, true);
                SetWaitingOnResponse(true);
                enHelperActivityType ev = cmdUpdateFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
                if (ev == enHelperActivityType.NoSuchMyListFile && watched)
                {
                    // the file is not actually on the user list, so let's add it
                    // we do this by issueing the same command without the edit flag
                    cmdUpdateFile = new AniDBCommand_UpdateFile();
                    cmdUpdateFile.Init(animeID, episodeNumber, watched, false);
                    ev = cmdUpdateFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                        new UnicodeEncoding(true, false));
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
                cmdAddFile = new AniDBCommand_AddFile();
                cmdAddFile.Init(fileDataLocal, ServerSettings.AniDB_MyList_StorageState);
                SetWaitingOnResponse(true);
                ev = cmdAddFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
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
                cmdAddFile = new AniDBCommand_AddFile();
                cmdAddFile.Init(animeID, episodeNumber, ServerSettings.AniDB_MyList_StorageState);
                SetWaitingOnResponse(true);
                ev = cmdAddFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            // if the user already has this file on 
            if (ev == enHelperActivityType.FileAlreadyExists && cmdAddFile.FileData != null)
            {
                watchedDate = cmdAddFile.WatchedDate;
                return cmdAddFile.ReturnIsWatched;
            }

            return false;
        }

        internal bool MarkFileAsExternalStorage(string Hash, long FileSize)
        {
            if (!Login()) return false;

            enHelperActivityType ev = enHelperActivityType.NoSuchMyListFile;
            AniDBCommand_MarkFileAsExternal cmdMarkFileExternal = null;

            lock (lockAniDBConnections)
            {
                cmdMarkFileExternal = new AniDBCommand_MarkFileAsExternal();
                cmdMarkFileExternal.Init(Hash, FileSize);
                SetWaitingOnResponse(true);
                ev = cmdMarkFileExternal.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            return true;
        }

        internal bool MarkFileAsUnknown(string Hash, long FileSize)
        {
            if (!Login()) return false;

            enHelperActivityType ev = enHelperActivityType.NoSuchMyListFile;
            AniDBCommand_MarkFileAsUnknown cmdMarkFileUnknown = null;

            lock (lockAniDBConnections)
            {
                cmdMarkFileUnknown = new AniDBCommand_MarkFileAsUnknown();
                cmdMarkFileUnknown.Init(Hash, FileSize);
                SetWaitingOnResponse(true);
                ev = cmdMarkFileUnknown.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            return true;
        }

        public bool MarkFileAsDeleted(string hash, long fileSize)
        {
            if (!Login()) return false;

            enHelperActivityType ev = enHelperActivityType.NoSuchMyListFile;
            AniDBCommand_MarkFileAsDeleted cmdDelFile = null;

            lock (lockAniDBConnections)
            {
                cmdDelFile = new AniDBCommand_MarkFileAsDeleted();
                cmdDelFile.Init(hash, fileSize);
                SetWaitingOnResponse(true);
                ev = cmdDelFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            return true;
        }

        public bool DeleteFileFromMyList(string hash, long fileSize)
        {
            if (!ServerSettings.AniDB_MyList_AddFiles) return false;

            if (!Login()) return false;

            enHelperActivityType ev = enHelperActivityType.NoSuchMyListFile;
            AniDBCommand_DeleteFile cmdDelFile = null;

            lock (lockAniDBConnections)
            {
                cmdDelFile = new AniDBCommand_DeleteFile();
                cmdDelFile.Init(hash, fileSize);
                SetWaitingOnResponse(true);
                ev = cmdDelFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
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
                cmdDelFile = new AniDBCommand_DeleteFile();
                cmdDelFile.Init(fileID);
                SetWaitingOnResponse(true);
                ev = cmdDelFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            return true;
        }

        public SVR_AniDB_Anime GetAnimeInfoUDP(int animeID, bool forceRefresh)
        {
            SVR_AniDB_Anime anime = null;

            bool skip = true;
            if (forceRefresh)
                skip = false;
            else
            {
                anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) skip = false;
            }

            if (skip)
            {
                if (anime == null)
                    anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);

                return anime;
            }

            if (!Login()) return null;

            enHelperActivityType ev = enHelperActivityType.NoSuchAnime;
            AniDBCommand_GetAnimeInfo getAnimeCmd = null;

            lock (lockAniDBConnections)
            {
                getAnimeCmd = new AniDBCommand_GetAnimeInfo();
                getAnimeCmd.Init(animeID, forceRefresh);
                SetWaitingOnResponse(true);
                ev = getAnimeCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            if (ev == enHelperActivityType.GotAnimeInfo && getAnimeCmd.AnimeInfo != null)
            {
                // check for an existing record so we don't over write the description
                anime = RepoFactory.AniDB_Anime.GetByAnimeID(getAnimeCmd.AnimeInfo.AnimeID);
                if (anime == null) anime = new SVR_AniDB_Anime();

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
                getCharCmd = new AniDBCommand_GetCharacterInfo();
                getCharCmd.Init(charID, true);
                SetWaitingOnResponse(true);
                ev = getCharCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            AniDB_Character chr = null;
            if (ev == enHelperActivityType.GotCharInfo && getCharCmd.CharInfo != null)
            {
                chr = RepoFactory.AniDB_Character.GetByCharID(charID);
                if (chr == null) chr = new AniDB_Character();

                chr.PopulateFromUDP(getCharCmd.CharInfo);
                RepoFactory.AniDB_Character.Save(chr);
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
                getCmd = new AniDBCommand_GetGroup();
                getCmd.Init(groupID);
                SetWaitingOnResponse(true);
                ev = getCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            AniDB_ReleaseGroup relGroup = null;
            if (ev == enHelperActivityType.GotGroup && getCmd.Group != null)
            {
                relGroup = RepoFactory.AniDB_ReleaseGroup.GetByGroupID(groupID);
                if (relGroup == null) relGroup = new AniDB_ReleaseGroup();

                relGroup.Populate(getCmd.Group);
                RepoFactory.AniDB_ReleaseGroup.Save(relGroup);
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
                getCmd = new AniDBCommand_GetGroupStatus();
                getCmd.Init(animeID);
                SetWaitingOnResponse(true);
                ev = getCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            if (ev == enHelperActivityType.GotGroupStatus && getCmd.GrpStatusCollection != null)
            {
                // delete existing records


                RepoFactory.AniDB_GroupStatus.DeleteForAnime(animeID);

                // save the records
                foreach (Raw_AniDB_GroupStatus raw in getCmd.GrpStatusCollection.Groups)
                {
                    AniDB_GroupStatus grpstat = new AniDB_GroupStatus();
                    grpstat.Populate(raw);
                    RepoFactory.AniDB_GroupStatus.Save(grpstat);
                }

                // updated cached stats
                // we don't do it in the save method as it would be too many unecessary updates
                logger.Trace("Updating group stats by anime from GetReleaseGroupStatusUDP: {0}", animeID);


                // StatsCache.Instance.UpdateUsingAnime(animeID); 
                //Removed QueueUpdateStatus will Update Groups


                if (getCmd.GrpStatusCollection.LatestEpisodeNumber > 0)
                {
                    // update the anime with a record of the latest subbed episode
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                    if (anime != null)
                    {
                        anime.LatestEpisodeNumber = getCmd.GrpStatusCollection.LatestEpisodeNumber;
                        RepoFactory.AniDB_Anime.Save(anime);

                        // check if we have this episode in the database
                        // if not get it now by updating the anime record
                        List<AniDB_Episode> eps = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeNumber(animeID,
                            getCmd.GrpStatusCollection.LatestEpisodeNumber);
                        if (eps.Count == 0)
                        {
                            CommandRequest_GetAnimeHTTP cr_anime =
                                new CommandRequest_GetAnimeHTTP(animeID, true, false);
                            cr_anime.Save();
                        }
                        // update the missing episode stats on groups and children
                        SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                        if (series != null)
                        {
                            series.QueueUpdateStats();
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
                cmd = new AniDBCommand_GetCalendar();
                cmd.Init();
                SetWaitingOnResponse(true);
                ev = cmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
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
                cmd = new AniDBCommand_GetReview();
                cmd.Init(reviewID);
                SetWaitingOnResponse(true);
                ev = cmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            AniDB_Review review = null;
            if (ev == enHelperActivityType.GotReview && cmd.ReviewInfo != null)
            {
                review = RepoFactory.AniDB_Review.GetByReviewID(reviewID);
                if (review == null) review = new AniDB_Review();

                review.Populate(cmd.ReviewInfo);
                RepoFactory.AniDB_Review.Save(review);
            }

            return review;
        }

        public bool VoteAnime(int animeID, decimal voteValue, AniDBVoteType voteType)
        {
            if (!Login()) return false;

            enHelperActivityType ev = enHelperActivityType.NoSuchVote;
            AniDBCommand_Vote cmdVote = null;


            lock (lockAniDBConnections)
            {
                cmdVote = new AniDBCommand_Vote();
                cmdVote.Init(animeID, voteValue, voteType);
                SetWaitingOnResponse(true);
                ev = cmdVote.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
                if (ev == enHelperActivityType.Voted || ev == enHelperActivityType.VoteUpdated)
                {
                    AniDB_Vote thisVote = RepoFactory.AniDB_Vote.GetByEntityAndType(cmdVote.EntityID, voteType);

                    if (thisVote == null)
                    {
                        thisVote = new AniDB_Vote();
                        thisVote.EntityID = cmdVote.EntityID;
                    }
                    thisVote.VoteType = (int) cmdVote.VoteType;
                    thisVote.VoteValue = cmdVote.VoteValue;
                    RepoFactory.AniDB_Vote.Save(thisVote);
                }
            }

            return false;
        }

        public void VoteAnimeRevoke(int animeID, AniDBVoteType voteType)
        {
            VoteAnime(animeID, -1, voteType);
        }


        public SVR_AniDB_Anime GetAnimeInfoHTTP(int animeID)
        {
            return GetAnimeInfoHTTP(animeID, false, true);
        }

        public SVR_AniDB_Anime GetAnimeInfoHTTP(int animeID, bool forceRefresh, bool downloadRelations)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetAnimeInfoHTTP(session, animeID, forceRefresh, downloadRelations);
            }
        }

        public SVR_AniDB_Anime GetAnimeInfoHTTP(ISession session, int animeID, bool forceRefresh,
            bool downloadRelations)
        {
            //if (!Login()) return null;

            SVR_AniDB_Anime anime = null;
            ISessionWrapper sessionWrapper = session.Wrap();

            bool skip = true;
            if (forceRefresh)
                skip = false;
            else
            {
                anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, animeID);
                if (anime == null) skip = false;
            }

            if (skip)
            {
                if (anime == null)
                    anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, animeID);

                return anime;
            }

            AniDBHTTPCommand_GetFullAnime getAnimeCmd = null;
            lock (lockAniDBConnections)
            {
                getAnimeCmd = new AniDBHTTPCommand_GetFullAnime();
                getAnimeCmd.Init(animeID, false, forceRefresh, false);
                getAnimeCmd.Process();
            }


            if (getAnimeCmd.Anime != null)
            {
                anime = SaveResultsForAnimeXML(session, animeID, downloadRelations, getAnimeCmd);
                //this endpoint is not working, so comenting...
/*
                if (forceRefresh)
                {
                    CommandRequest_Azure_SendAnimeFull cmdAzure = new CommandRequest_Azure_SendAnimeFull(anime.AnimeID);
                    cmdAzure.Save(session);
                }*/
            }

            return anime;
        }

        private SVR_AniDB_Anime SaveResultsForAnimeXML(ISession session, int animeID, bool downloadRelations,
            AniDBHTTPCommand_GetFullAnime getAnimeCmd)
        {
            SVR_AniDB_Anime anime = null;
            ISessionWrapper sessionWrapper = session.Wrap();

            logger.Trace("cmdResult.Anime: {0}", getAnimeCmd.Anime);

            anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, animeID);
            if (anime == null)
                anime = new SVR_AniDB_Anime();
            anime.PopulateAndSaveFromHTTP(session, getAnimeCmd.Anime, getAnimeCmd.Episodes, getAnimeCmd.Titles,
                getAnimeCmd.Categories, getAnimeCmd.Tags,
                getAnimeCmd.Characters, getAnimeCmd.Relations, getAnimeCmd.SimilarAnime, getAnimeCmd.Recommendations,
                downloadRelations);

            // Request an image download
            CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(anime.AniDB_AnimeID,
                JMMImageType.AniDB_Cover,
                false);
            cmd.Save(session);
            // create AnimeEpisode records for all episodes in this anime
            // only if we have a series
            SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
            RepoFactory.AniDB_Anime.Save(anime);
            if (ser != null)
            {
                ser.CreateAnimeEpisodes(session);
                RepoFactory.AnimeSeries.Save(ser, true, false);
            }

            // update any files, that may have been linked
            /*CrossRef_File_EpisodeRepository repCrossRefs = new CrossRef_File_EpisodeRepository();
            repCrossRefs.GetByAnimeID(*/

            // update cached stats

            //StatsCache.Instance.UpdateUsingAnime(session, anime.AnimeID);
            //StatsCache.Instance.UpdateAnimeContract(session, anime.AnimeID);

            // download character images
            foreach (AniDB_Anime_Character animeChar in anime.GetAnimeCharacters(session.Wrap()))
            {
                AniDB_Character chr = animeChar.GetCharacter(sessionWrapper);
                if (chr == null) continue;

                if (ServerSettings.AniDB_DownloadCharacters)
                {
                    if (!string.IsNullOrEmpty(chr.GetPosterPath()) && !File.Exists(chr.GetPosterPath()))
                    {
                        logger.Debug("Downloading character image: {0} - {1}({2}) - {3}", anime.MainTitle, chr.CharName,
                            chr.CharID,
                            chr.GetPosterPath());
                        cmd = new CommandRequest_DownloadImage(chr.AniDB_CharacterID, JMMImageType.AniDB_Character,
                            false);
                        cmd.Save();
                    }
                }

                if (ServerSettings.AniDB_DownloadCreators)
                {
                    AniDB_Seiyuu seiyuu = chr.GetSeiyuu(session);
                    if (seiyuu == null || string.IsNullOrEmpty(seiyuu.GetPosterPath())) continue;

                    if (!File.Exists(seiyuu.GetPosterPath()))
                    {
                        logger.Debug("Downloading seiyuu image: {0} - {1}({2}) - {3}", anime.MainTitle,
                            seiyuu.SeiyuuName, seiyuu.SeiyuuID,
                            seiyuu.GetPosterPath());
                        cmd =
                            new CommandRequest_DownloadImage(seiyuu.AniDB_SeiyuuID, JMMImageType.AniDB_Creator, false);
                        cmd.Save();
                    }
                }
            }

            return anime;
        }

        public SVR_AniDB_Anime GetAnimeInfoHTTPFromCache(ISession session, int animeID, bool downloadRelations)
        {
            AniDBHTTPCommand_GetFullAnime getAnimeCmd = null;
            lock (lockAniDBConnections)
            {
                getAnimeCmd = new AniDBHTTPCommand_GetFullAnime();
                getAnimeCmd.Init(animeID, false, false, true);
                getAnimeCmd.Process();
            }

            SVR_AniDB_Anime anime = null;
            if (getAnimeCmd.Anime != null)
            {
                anime = SaveResultsForAnimeXML(session, animeID, downloadRelations, getAnimeCmd);
            }
            return anime;
        }

        public bool ValidAniDBCredentials()
        {
            if (string.IsNullOrEmpty(this.userName) || string.IsNullOrEmpty(this.password) ||
                string.IsNullOrEmpty(this.serverName)
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

            logger.Info("BindToLocalPort: Bound to local address: {0} - Port: {1} ({2})", localIpEndPoint.ToString(),
                clientPort,
                localIpEndPoint.AddressFamily);

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

                logger.Info("BindToRemotePort: Bound to remote address: " + remoteIpEndPoint.Address.ToString() +
                            " : " +
                            remoteIpEndPoint.Port.ToString());

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Could not bind to remote port: {ex}");
                return false;
            }
        }
    }
}