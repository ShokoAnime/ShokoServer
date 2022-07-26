using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;
using AniDBAPI;
using AniDBAPI.Commands;
using NHibernate;
using NLog;
using Shoko.Commons.Properties;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Timer = System.Timers.Timer;

namespace Shoko.Server.AniDB_API
{
    public class AniDBHelper
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // we use this lock to make don't try and access AniDB too much (UDP and HTTP)
        private readonly object lockAniDBConnections = new object();

        internal static readonly int HTTPBanTimerResetLength = 12;

        internal static readonly int UDPBanTimerResetLength = 12;

        private IPEndPoint localIpEndPoint;
        private IPEndPoint remoteIpEndPoint;
        private Socket soUdp;
        private string curSessionID = string.Empty;
        private string curImageServerUrl;

        private string userName = string.Empty;
        private string password = string.Empty;
        private string serverName = string.Empty;
        private string serverPort = string.Empty;
        private string clientPort = string.Empty;

        private Timer logoutTimer;

        private Timer httpBanResetTimer;
        private Timer udpBanResetTimer;

        public DateTime? HttpBanTime { get; set; }
        public DateTime? UdpBanTime { get; set; }

        internal event EventHandler<AniDBStateUpdate> AniDBStateUpdate;

        public string ImageServerUrl {
            get {
                if (string.IsNullOrWhiteSpace(curImageServerUrl))
                {
                    if (!Login())
                    {
                        //Don't keep trying after a failed login set to constant
                        curImageServerUrl = Constants.URLS.AniDB_Images_Domain;
                    } 
                    else
                    {                        
                        if (string.IsNullOrWhiteSpace(curImageServerUrl))
                        {
                            //The API call did not return anything useful, don't try again.
                            curImageServerUrl = Constants.URLS.AniDB_Images_Domain;
                        }
                    }
                }
                var url = string.Format(Constants.URLS.AniDB_Images, curImageServerUrl);
                return url;
            }
        }

        private bool _isHttpBanned;
        private bool _isUdpBanned;

        public bool IsHttpBanned
        {
            get => _isHttpBanned;
            set
            {
                _isHttpBanned = value;
                if (value)
                {
                    HttpBanTime = DateTime.Now;
                    ServerInfo.Instance.IsBanned = true;
                    ServerInfo.Instance.BanOrigin = @"HTTP";
                    ServerInfo.Instance.BanReason = HttpBanTime.ToString();
                    if (httpBanResetTimer.Enabled)
                    {
                        logger.Warn("HTTP ban timer was already running, ban time extending");
                        httpBanResetTimer.Stop(); //re-start implies stop
                    }
                    httpBanResetTimer.Start();
                    Analytics.PostEvent("AniDB", "Http Banned");
                    ShokoEventHandler.Instance.OnAniDBBanned(AniDBBanType.HTTP, HttpBanTime.Value, HttpBanTime.Value.AddHours(HTTPBanTimerResetLength));
                }
                else
                {
                    if (httpBanResetTimer.Enabled)
                    {
                        httpBanResetTimer.Stop();
                        logger.Info("HTTP ban timer stopped. Resuming queue if not paused.");
                        // Skip if paused
                        if (!ShokoService.CmdProcessorGeneral.Paused)
                        {
                            // Needs to have something to do first
                            if (ShokoService.CmdProcessorGeneral.QueueCount > 0)
                            {
                                // Not really a new command, but this will start the queue if it's not running,
                                // with handling for problems
                                ShokoService.CmdProcessorGeneral.NotifyOfNewCommand();
                            }
                        }
                    }
                    if (!IsUdpBanned)
                    {
                        ServerInfo.Instance.IsBanned = false;
                        ServerInfo.Instance.BanOrigin = string.Empty;
                        ServerInfo.Instance.BanReason = string.Empty;
                    }
                }
                
                AniDBStateUpdate?.Invoke(this, new AniDBStateUpdate
                {
                    Value = value,
                    UpdateTime = HttpBanTime.Value,
                    UpdateType = UpdateType.HTTPBan,
                    PauseTimeSecs = HTTPBanTimerResetLength,
                });
            }
        }

        public bool IsUdpBanned
        {
            get => _isUdpBanned;
            set
            {
                _isUdpBanned = value;
                if (value)
                {
                    UdpBanTime = DateTime.Now;
                    ServerInfo.Instance.IsBanned = true;
                    ServerInfo.Instance.BanOrigin = @"UDP";
                    ServerInfo.Instance.BanReason = UdpBanTime.ToString();
                    if (udpBanResetTimer.Enabled)
                    {
                        logger.Warn("UDP ban timer was already running, ban time extending");
                        udpBanResetTimer.Stop(); // re-start implies stop
                    }
                    udpBanResetTimer.Start();
                    Analytics.PostEvent("AniDB", "Udp Banned");
                    ShokoEventHandler.Instance.OnAniDBBanned(AniDBBanType.UDP, UdpBanTime.Value, UdpBanTime.Value.AddHours(UDPBanTimerResetLength));
                }
                else
                {
                    if (udpBanResetTimer.Enabled)
                    {
                        udpBanResetTimer.Stop();
                        logger.Info("UDP ban timer stopped. Resuming if not Paused");
                        // Skip if paused
                        if (!ShokoService.CmdProcessorGeneral.Paused)
                        {
                            // Needs to have something to do first
                            if (ShokoService.CmdProcessorGeneral.QueueCount > 0)
                            {
                                // Not really a new command, but this will start the queue if it's not running,
                                // with handling for problems
                                ShokoService.CmdProcessorGeneral.NotifyOfNewCommand();
                            }
                        }
                    }
                    if (!IsHttpBanned)
                    {
                        ServerInfo.Instance.IsBanned = false;
                        ServerInfo.Instance.BanOrigin = string.Empty;
                        ServerInfo.Instance.BanReason = string.Empty;
                    }
                }

                AniDBStateUpdate?.Invoke(this, new AniDBStateUpdate
                {
                    Value = value,
                    UpdateTime = UdpBanTime.Value,
                    UpdateType = UpdateType.UDPBan,
                    PauseTimeSecs = UDPBanTimerResetLength,
                });
            }
        }

        private bool isInvalidSession;

        public bool IsInvalidSession
        {
            get => isInvalidSession;

            set
            {
                isInvalidSession = value;
                ServerInfo.Instance.IsInvalidSession = isInvalidSession;
                AniDBStateUpdate?.Invoke(this, new AniDBStateUpdate
                {
                    Value = value,
                    UpdateTime = DateTime.Now,
                    UpdateType = UpdateType.InvalidSession,
                });
            }
        }

        private bool isLoggedOn;

        public bool IsLoggedOn
        {
            get => isLoggedOn;
            set => isLoggedOn = value;
        }

        public bool WaitingOnResponse { get; set; }

        public DateTime? WaitingOnResponseTime { get; set; }

        public int? ExtendPauseSecs { get; set; }

        public bool IsNetworkAvailable { private set; get; }

        public string ExtendPauseReason { get; set; } = string.Empty;

        public static event EventHandler LoginFailed;

        public void ExtendPause(int secsToPause, string pauseReason)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);

            ExtendPauseSecs = secsToPause;
            ExtendPauseReason = pauseReason;
            ServerInfo.Instance.ExtendedPauseString = string.Format(Resources.AniDB_Paused,
                secsToPause,
                pauseReason);
            ServerInfo.Instance.HasExtendedPause = true;
        }

        public void ResetExtendPause()
        {
            ExtendPauseSecs = null;
            ExtendPauseReason = string.Empty;
            ServerInfo.Instance.ExtendedPauseString = string.Empty;
            ServerInfo.Instance.HasExtendedPause = false;
        }

        public void Init(string userName, string password, string serverName, ushort serverPort, ushort clientPort)
        {
            soUdp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            this.userName = userName;
            this.password = password;
            this.serverName = serverName;
            this.serverPort = serverPort.ToString();
            this.clientPort = clientPort.ToString();

            isLoggedOn = false;

            if (!BindToLocalPort()) IsNetworkAvailable = false;
            if (!BindToRemotePort()) IsNetworkAvailable = false;

            logoutTimer = new Timer();
            logoutTimer.Elapsed += LogoutTimer_Elapsed;
            logoutTimer.Interval = 5000; // Set the Interval to 5 seconds.
            logoutTimer.Enabled = true;
            logoutTimer.AutoReset = true;

            logger.Info("starting logout timer...");
            logoutTimer.Start();

            httpBanResetTimer = new Timer();
            httpBanResetTimer.AutoReset = false;
            httpBanResetTimer.Elapsed += HTTPBanResetTimerElapsed;
            httpBanResetTimer.Interval = TimeSpan.FromHours(HTTPBanTimerResetLength).TotalMilliseconds;

            udpBanResetTimer = new Timer();
            udpBanResetTimer.AutoReset = false;
            udpBanResetTimer.Elapsed += UDPBanResetTimerElapsed;
            udpBanResetTimer.Interval = TimeSpan.FromHours(UDPBanTimerResetLength).TotalMilliseconds;
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
            try{
                soUdp.Shutdown(SocketShutdown.Both);
                if (soUdp.Connected) {
                    soUdp.Disconnect(false);
                }
            }
            catch (SocketException ex) {
                logger.Error($"Failed to Shutdown and Disconnect the connection to AniDB: {ex}");
            }
            finally {
                logger.Info("CLOSING ANIDB CONNECTION...");
                soUdp.Close();
                logger.Info("CLOSED ANIDB CONNECTION");
                soUdp = null;
            }
        }

        void LogoutTimer_Elapsed(object sender, ElapsedEventArgs e)
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
                        Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);

                        TimeSpan ts = DateTime.Now - WaitingOnResponseTime.Value;
                        ServerInfo.Instance.WaitingOnResponseAniDBUDPString =
                            string.Format(Resources.AniDB_ResponseWaitSeconds,
                                ts.TotalSeconds);
                    }
                }
                catch
                {
                    //IGNORE
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
                    !IsUdpBanned && !ExtendPauseSecs.HasValue)
                {
                    AniDBCommand_Ping ping = new AniDBCommand_Ping();
                    ping.Init();
                    ping.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                }

                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);

                string msg = string.Format(Resources.AniDB_LastMessage,
                    tsAniDBUDP.TotalSeconds);

                if (tsAniDBNonPing.TotalSeconds > Constants.ForceLogoutPeriod) // after 10 minutes
                {
                    ForceLogout();
                }
            }
        }

        private void HTTPBanResetTimerElapsed(object sender, ElapsedEventArgs e)
        {
            logger.Info("HTTP ban (12h) is over");
            IsHttpBanned = false;
        }

        private void UDPBanResetTimerElapsed(object sender, ElapsedEventArgs e)
        {
            logger.Info("UDP ban (12h) is over");
            IsUdpBanned = false;
        }

        private void SetWaitingOnResponse(bool isWaiting)
        {
            WaitingOnResponse = isWaiting;
            ServerInfo.Instance.WaitingOnResponseAniDBUDP = isWaiting;

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);

            if (isWaiting)
                ServerInfo.Instance.WaitingOnResponseAniDBUDPString =
                    Resources.AniDB_ResponseWait;
            else
                ServerInfo.Instance.WaitingOnResponseAniDBUDPString = Resources.Command_Idle;

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

            if (remoteIpEndPoint == null) return false;
            if (soUdp == null) return false;

            AniDBCommand_Login login = new AniDBCommand_Login();
            login.Init(userName, password);

            string msg = login.commandText.Replace(userName, "******");
            msg = msg.Replace(password, "******");
            logger.Trace("udp command: {0}", msg);
            SetWaitingOnResponse(true);
            AniDBUDPResponseCode ev = login.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                new UnicodeEncoding(true, false));
            SetWaitingOnResponse(false);

            if (login.errorOccurred)
                logger.Trace("error in login: {0}", login.errorMessage);
            //else
            //  logger.Info("socketResponse: {0}", login.socketResponse);

            Thread.Sleep(2200);

            switch (ev)
            {
                case AniDBUDPResponseCode.LoginFailed:
                    logger.Error("AniDB Login Failed: invalid credentials");
                    LoginFailed?.Invoke(this, null);
                    break;
                case AniDBUDPResponseCode.LoggedIn:
                    curSessionID = login.SessionID;
                    curImageServerUrl = login.ImageServerUrl;
                    isLoggedOn = true;
                    IsInvalidSession = false;
                    return true;
                default:
                    logger.Error($"AniDB Login Failed: error connecting to AniDB: {login.errorMessage}");
                    break;
            }

            return false;
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

        public Raw_AniDB_File GetFileInfo(IHash vidLocal)
        {
            if (!Login()) return null;

            AniDBUDPResponseCode ev = AniDBUDPResponseCode.NoSuchFile;
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

            if (ev == AniDBUDPResponseCode.GotFileInfo && getInfoCmd.fileInfo != null)
            {
                try
                {
                    logger.Trace("ProcessResult_GetFileInfo: {0}", getInfoCmd.fileInfo);

                    if (ServerSettings.Instance.AniDb.DownloadReleaseGroups)
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
            if (!ServerSettings.Instance.AniDb.MyList_ReadWatched) return;

            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                AniDBCommand_GetMyListFileInfo cmdGetFileStatus = new AniDBCommand_GetMyListFileInfo();
                cmdGetFileStatus.Init(aniDBFileID);
                SetWaitingOnResponse(true);
                AniDBUDPResponseCode ev = cmdGetFileStatus.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
                switch (ev)
                {
                        case AniDBUDPResponseCode.Banned_555:
                            logger.Error("Recieved ban on trying to get MyList stats for file");
                            return;
                        // Ignore no info in MyList for file
                        case AniDBUDPResponseCode.NoSuchMyListFile: return;
                        case AniDBUDPResponseCode.LoginRequired:
                            logger.Error("Not logged in to AniDB");
                            return;
                }
                if (cmdGetFileStatus.MyListFile?.WatchedDate == null) return;
                var aniFile = RepoFactory.AniDB_File.GetByFileID(aniDBFileID);
                var vids = aniFile.EpisodeIDs.SelectMany(a => RepoFactory.VideoLocal.GetByAniDBEpisodeID(a)).Where(a => a != null).ToList();
                foreach (var vid in vids)
                {
                    foreach (var user in RepoFactory.JMMUser.GetAniDBUsers())
                    {
                        vid.ToggleWatchedStatus(true, false, cmdGetFileStatus.MyListFile.WatchedDate, true,
                            user.JMMUserID, false, true);
                    }
                }
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
                AniDBUDPResponseCode ev = cmdGetMylistStats.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
                if (ev == AniDBUDPResponseCode.GotMyListStats && cmdGetMylistStats.MyListStats != null)
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

        /// <summary>
        /// Gets the list of updated AnimeIDs. This may use more than one call to get them all, and therefore a lot of time
        /// </summary>
        /// <param name="updatedAnimeIDs">The updated IDs</param>
        /// <param name="lastUpdateTime">The time that the last anime was updated</param>
        public void GetUpdated(ref List<int> updatedAnimeIDs, ref long lastUpdateTime)
        {
            updatedAnimeIDs = new List<int>();

            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                AniDBCommand_GetUpdated cmdUpdated = new AniDBCommand_GetUpdated();
                cmdUpdated.Init(lastUpdateTime.ToString());
                SetWaitingOnResponse(true);
                AniDBUDPResponseCode ev = cmdUpdated.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);

                if (ev != AniDBUDPResponseCode.GotUpdated) return;

                int records = cmdUpdated.RecordCount;
                if (records <= 0) return;

                lastUpdateTime = long.Parse(cmdUpdated.LastUpdateTime);
                updatedAnimeIDs.AddRange(cmdUpdated.AnimeIDList);

                // we got them all
                if (records <= 200) return;

                // while loop it to be sure
                while (records > 200)
                {
                    // reinit with last update time provided
                    cmdUpdated = new AniDBCommand_GetUpdated();
                    cmdUpdated.Init(lastUpdateTime.ToString());
                    // get the rest (assuming RecordCount was <= 400
                    SetWaitingOnResponse(true);
                    ev = cmdUpdated.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                        new UnicodeEncoding(true, false));
                    SetWaitingOnResponse(false);

                    if (ev != AniDBUDPResponseCode.GotUpdated) return;

                    // update records with new count
                    records = cmdUpdated.RecordCount;
                    if (records <= 0) return;

                    lastUpdateTime = long.Parse(cmdUpdated.LastUpdateTime);

                    // if the first/last item overlap, then remove it so it isn't duplicated
                    if (cmdUpdated.AnimeIDList.Count > 0 && cmdUpdated.AnimeIDList[0] == updatedAnimeIDs.LastOrDefault())
                        cmdUpdated.AnimeIDList.RemoveAt(0);

                    updatedAnimeIDs.AddRange(cmdUpdated.AnimeIDList);
                }
            }
        }

        /// <summary>
        /// This is not for generic files (manually linked)
        /// </summary>
        /// <param name="animeID"></param>
        /// <param name="episodeNumber"></param>
        /// <param name="watched"></param>
        public void UpdateMyListFileStatus(IServiceProvider provider, IHash hash, bool watched, DateTime? watchedDate = null)
        {
            if (!ServerSettings.Instance.AniDb.MyList_AddFiles) return;

            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                if (watched && watchedDate == null) watchedDate = DateTime.Now;

                AniDBUDPResponseCode ev;
                // We have the ID, so update it
                if (hash.MyListID > 0)
                {
                    AniDBCommand_UpdateFile cmdUpdateFile = new AniDBCommand_UpdateFile();
                    cmdUpdateFile.Init(hash, watched, watchedDate);
                    SetWaitingOnResponse(true);
                    ev = cmdUpdateFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                        new UnicodeEncoding(true, false));
                    SetWaitingOnResponse(false);
                }
                else
                {
                    logger.Trace($"File has no MyListID, attempting to add: {hash.ED2KHash}");
                    // We don't have the MyListID, so we'll act like it's not there, and AniDB will tell us
                    ev = AniDBUDPResponseCode.NoSuchMyListFile;
                }

                if (ev == AniDBUDPResponseCode.NoSuchMyListFile)
                {
                    // Run synchronously, but still do all of the stuff with Trakt and whatnot
                    // We are skipping the watched state settings, as we are setting them here
                    CommandRequest_AddFileToMyList addcmd = new CommandRequest_AddFileToMyList(hash.ED2KHash, false);
                    // Initialize private parts
                    addcmd.LoadFromDBCommand(addcmd.ToDatabaseObject());
                    addcmd.ProcessCommand(provider);
                }
            }
        }

        /// <summary>
        /// This is for generic files (manually linked)
        /// </summary>
        /// <param name="animeID"></param>
        /// <param name="episodeNumber"></param>
        /// <param name="watched"></param>
        public void UpdateMyListFileStatus(IServiceProvider provider, IHash hash, int animeID, int episodeNumber, bool watched, DateTime? watchedDate = null)
        {
            if (!ServerSettings.Instance.AniDb.MyList_AddFiles) return;

            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                if (watched && watchedDate == null) watchedDate = DateTime.Now;

                AniDBCommand_UpdateFile cmdUpdateFile = new AniDBCommand_UpdateFile();
                cmdUpdateFile.Init(hash, animeID, episodeNumber, watched, watchedDate);
                SetWaitingOnResponse(true);
                var ev = cmdUpdateFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);

                if (ev == AniDBUDPResponseCode.NoSuchMyListFile)
                {
                    // Run synchronously, but still do all of the stuff with Trakt and whatnot
                    // We are skipping the watched state settings, as we are setting them here
                    CommandRequest_AddFileToMyList addcmd = new CommandRequest_AddFileToMyList(hash.ED2KHash, false);
                    // Initialize private parts
                    addcmd.LoadFromDBCommand(addcmd.ToDatabaseObject());
                    addcmd.ProcessCommand(provider);
                }
            }
        }

        public (int?, DateTime?) AddFileToMyList(IHash fileDataLocal, DateTime? watchedDate, ref AniDBFile_State? state)
        {
            // It's easier to compare a change if we return the original watch date instead of null, since null means unwatched
            if (!ServerSettings.Instance.AniDb.MyList_AddFiles) return (null, watchedDate);

            if (!Login()) return (null, watchedDate);

            AniDBUDPResponseCode ev;
            AniDBCommand_AddFile cmdAddFile;

            lock (lockAniDBConnections)
            {
                cmdAddFile = new AniDBCommand_AddFile();
                cmdAddFile.Init(fileDataLocal, ServerSettings.Instance.AniDb.MyList_StorageState, watchedDate);
                SetWaitingOnResponse(true);
                ev = cmdAddFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            // if the user already has this file on
            if (ev == AniDBUDPResponseCode.FileAlreadyExists && cmdAddFile.FileData != null)
            {
                state = cmdAddFile.State;
                return (cmdAddFile.MyListID, cmdAddFile.WatchedDate);
            }

            if (cmdAddFile.MyListID > 0) return (cmdAddFile.MyListID, watchedDate);

            return (null, watchedDate);
        }

        public (int?, DateTime?) AddFileToMyList(int animeID, int episodeNumber, DateTime? watchedDate, ref AniDBFile_State? state)
        {
            if (!ServerSettings.Instance.AniDb.MyList_AddFiles) return (null, watchedDate);
            // It's easier to compare a change if we return the original watch date instead of null, since null means unwatched
            if (!Login()) return (null, watchedDate);

            AniDBUDPResponseCode ev;
            AniDBCommand_AddFile cmdAddFile;

            lock (lockAniDBConnections)
            {
                cmdAddFile = new AniDBCommand_AddFile();
                cmdAddFile.Init(animeID, episodeNumber, ServerSettings.Instance.AniDb.MyList_StorageState, watchedDate);
                SetWaitingOnResponse(true);
                ev = cmdAddFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            // if the user already has this file on
            if (ev == AniDBUDPResponseCode.FileAlreadyExists && cmdAddFile.FileData != null)
            {
                state = cmdAddFile.State;
                return (cmdAddFile.MyListID, cmdAddFile.WatchedDate);
            }

            if (cmdAddFile.MyListID > 0) return (cmdAddFile.MyListID, watchedDate);

            return (null, watchedDate);
        }

        internal void MarkFileAsRemote(int myListID)
        {
            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                var cmdMarkFileExternal = new AniDBCommand_MarkFileAsRemote();
                cmdMarkFileExternal.Init(myListID);
                SetWaitingOnResponse(true);
                cmdMarkFileExternal.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }
        }

        internal void MarkFileAsOnDisk(int myListID)
        {
            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                var cmdMarkFileDisk = new AniDBCommand_MarkFileAsDisk();
                cmdMarkFileDisk.Init(myListID);
                SetWaitingOnResponse(true);
                cmdMarkFileDisk.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }
        }

        public void MarkFileAsUnknown(int myListID)
        {
            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                var cmdMarkFileUnknown = new AniDBCommand_MarkFileAsUnknown();
                cmdMarkFileUnknown.Init(myListID);
                SetWaitingOnResponse(true);
                cmdMarkFileUnknown.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }
        }

        public void MarkFileAsDeleted(int myListID)
        {
            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                var cmdDelFile = new AniDBCommand_MarkFileAsDeleted();
                cmdDelFile.Init(myListID);
                SetWaitingOnResponse(true);
                cmdDelFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }
        }

        public void DeleteFileFromMyList(string hash, long fileSize)
        {
            if (!ServerSettings.Instance.AniDb.MyList_AddFiles) return;

            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                var cmdDelFile = new AniDBCommand_DeleteFile();
                cmdDelFile.Init(hash, fileSize);
                SetWaitingOnResponse(true);
                cmdDelFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }
        }

        public void DeleteFileFromMyList(int fileID)
        {
            if (!ServerSettings.Instance.AniDb.MyList_AddFiles) return;

            if (!Login()) return;

            lock (lockAniDBConnections)
            {
                var cmdDelFile = new AniDBCommand_DeleteFile();
                cmdDelFile.Init(fileID);
                SetWaitingOnResponse(true);
                cmdDelFile.Process(ref soUdp, ref remoteIpEndPoint, curSessionID,
                    new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }
        }

        public void GetReleaseGroupUDP(int groupID)
        {
            if (!Login()) return;

            AniDBUDPResponseCode ev;
            AniDBCommand_GetGroup getCmd;
            lock (lockAniDBConnections)
            {
                getCmd = new AniDBCommand_GetGroup();
                getCmd.Init(groupID);
                SetWaitingOnResponse(true);
                ev = getCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            if (ev != AniDBUDPResponseCode.GotGroup || getCmd.Group == null) return;
            var relGroup = RepoFactory.AniDB_ReleaseGroup.GetByGroupID(groupID) ?? new AniDB_ReleaseGroup();

            relGroup.Populate(getCmd.Group);
            RepoFactory.AniDB_ReleaseGroup.Save(relGroup);
        }

        public GroupStatusCollection GetReleaseGroupStatusUDP(int animeID)
        {
            if (!Login()) return null;

            AniDBUDPResponseCode ev;
            AniDBCommand_GetGroupStatus getCmd;
            lock (lockAniDBConnections)
            {
                getCmd = new AniDBCommand_GetGroupStatus();
                getCmd.Init(animeID);
                SetWaitingOnResponse(true);
                ev = getCmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            if (ev != AniDBUDPResponseCode.GotGroupStatus || getCmd.GrpStatusCollection == null)
                return getCmd.GrpStatusCollection;

            // delete existing records
            RepoFactory.AniDB_GroupStatus.DeleteForAnime(animeID);

            // save the records
            foreach (Raw_AniDB_GroupStatus raw in getCmd.GrpStatusCollection.Groups)
            {
                AniDB_GroupStatus grpstat = new AniDB_GroupStatus();
                grpstat.Populate(raw);
                RepoFactory.AniDB_GroupStatus.Save(grpstat);
            }

            if (getCmd.GrpStatusCollection.LatestEpisodeNumber > 0)
            {
                // update the anime with a record of the latest subbed episode
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return getCmd.GrpStatusCollection;

                anime.LatestEpisodeNumber = getCmd.GrpStatusCollection.LatestEpisodeNumber;
                RepoFactory.AniDB_Anime.Save(anime, false);

                // check if we have this episode in the database
                // if not get it now by updating the anime record
                List<AniDB_Episode> eps = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeNumber(animeID,
                    getCmd.GrpStatusCollection.LatestEpisodeNumber);
                if (eps.Count == 0)
                {
                    CommandRequest_GetAnimeHTTP cr_anime =
                        new CommandRequest_GetAnimeHTTP(animeID, true, false, false);
                    cr_anime.Save();
                }
                // update the missing episode stats on groups and children
                SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                series?.QueueUpdateStats();
            }

            return getCmd.GrpStatusCollection;
        }

        public CalendarCollection GetCalendarUDP()
        {
            if (!Login()) return null;

            AniDBUDPResponseCode ev = AniDBUDPResponseCode.CalendarEmpty;
            AniDBCommand_GetCalendar cmd = null;
            lock (lockAniDBConnections)
            {
                cmd = new AniDBCommand_GetCalendar();
                cmd.Init();
                SetWaitingOnResponse(true);
                ev = cmd.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
            }

            if (ev == AniDBUDPResponseCode.GotCalendar && cmd.Calendars != null)
                return cmd.Calendars;

            return null;
        }

        public void VoteAnime(int animeID, decimal voteValue, AniDBVoteType voteType)
        {
            if (!Login()) return;


            lock (lockAniDBConnections)
            {
                var cmdVote = new AniDBCommand_Vote();
                cmdVote.Init(animeID, voteValue, voteType);
                SetWaitingOnResponse(true);
                var ev = cmdVote.Process(ref soUdp, ref remoteIpEndPoint, curSessionID, new UnicodeEncoding(true, false));
                SetWaitingOnResponse(false);
                if (ev != AniDBUDPResponseCode.Voted && ev != AniDBUDPResponseCode.VoteUpdated) return;
                AniDB_Vote thisVote = RepoFactory.AniDB_Vote.GetByEntityAndType(cmdVote.EntityID, voteType) ?? new AniDB_Vote
                {
                    EntityID = cmdVote.EntityID
                };

                thisVote.VoteType = (int) cmdVote.VoteType;
                thisVote.VoteValue = cmdVote.VoteValue;
                RepoFactory.AniDB_Vote.Save(thisVote);
            }
        }

        public bool UpdateCachedAnimeInfoHTTP(SVR_AniDB_Anime anime, bool createSeriesEntry = false)
        {
            if (anime == null)
                return false;
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var animeID = anime.AnimeID;
                AniDBHTTPCommand_GetFullAnime getAnimeCmd;

                lock (lockAniDBConnections)
                {
                    getAnimeCmd = new AniDBHTTPCommand_GetFullAnime();
                    getAnimeCmd.Init(animeID, false, false, true);
                    var result = getAnimeCmd.Process();
                    if (result == AniDBUDPResponseCode.NoSuchAnime)
                    {
                        logger.Error($"Failed get cached anime info for {animeID}. AniDB ban or No Such Anime returned");
                        return false;
                    }
                }

                if (getAnimeCmd.Anime == null)
                {
                    logger.Error($"Failed get cached anime info for {animeID}. Anime was null");
                    return false;
                }


                logger.Trace("cmdResult.Anime: {0}", getAnimeCmd.Anime);

                if (!anime.PopulateAndSaveFromHTTP(session, getAnimeCmd.Anime, getAnimeCmd.Episodes, getAnimeCmd.Titles, getAnimeCmd.Tags,
                    getAnimeCmd.Characters, getAnimeCmd.Staff, getAnimeCmd.Resources, getAnimeCmd.Relations, getAnimeCmd.SimilarAnime, getAnimeCmd.Recommendations,
                    false, 0, createSeriesEntry))
                {
                    logger.Error($"Failed populate cached anime info for {animeID}");
                    return false;
                }

                // create AnimeEpisode records for all episodes in this anime only if we have a series
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                if (ser != null)
                {
                    ser.CreateAnimeEpisodes(session, anime);
                    RepoFactory.AnimeSeries.Save(ser, true, false);
                }
                SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);
            }
            return true;
        }

        public SVR_AniDB_Anime GetAnimeInfoHTTP(int animeID, bool forceRefresh = false, bool downloadRelations = true, int relDepth = 0, bool createSeriesEntry = false)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetAnimeInfoHTTP(session, animeID, forceRefresh, downloadRelations, relDepth, createSeriesEntry);
            }
        }

        public SVR_AniDB_Anime GetAnimeInfoHTTP(ISession session, int animeID, bool forceRefresh,
            bool downloadRelations, int relDepth = 0, bool createSeriesEntry = false)
        {
            //if (!Login()) return null;

            ISessionWrapper sessionWrapper = session.Wrap();

            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, animeID);
            var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(animeID);
            bool skip = true;
            bool animeRecentlyUpdated = false;
            if (anime != null && update != null)
            {
                TimeSpan ts = DateTime.Now - update.UpdatedAt;
                if (ts.TotalHours < 4) animeRecentlyUpdated = true;
            }
            if (!animeRecentlyUpdated)
            {
                if (forceRefresh)
                    skip = false;
                else if (anime == null) skip = false;
            }

            AniDBHTTPCommand_GetFullAnime getAnimeCmd;
            lock (lockAniDBConnections)
            {
                getAnimeCmd = new AniDBHTTPCommand_GetFullAnime();
                getAnimeCmd.Init(animeID, createSeriesEntry, !skip, animeRecentlyUpdated);
                var result = getAnimeCmd.Process();
                if (result == AniDBUDPResponseCode.Banned_555 || result == AniDBUDPResponseCode.NoSuchAnime)
                {
                    logger.Error($"Failed get anime info for {animeID}. AniDB ban or No Such Anime returned");
                    return null;
                }
            }


            if (getAnimeCmd.Anime != null)
            {
                return SaveResultsForAnimeXML(session, animeID, downloadRelations || ServerSettings.Instance.AutoGroupSeries, true, getAnimeCmd, relDepth, createSeriesEntry);
            }

            logger.Error($"Failed get anime info for {animeID}. Anime was null");
            return null;
        }

        public SVR_AniDB_Anime SaveResultsForAnimeXML(ISession session, int animeID, bool downloadRelations,
            bool validateImages,
            AniDBHTTPCommand_GetFullAnime getAnimeCmd, int relDepth, bool createSeriesEntry)
        {
            ISessionWrapper sessionWrapper = session.Wrap();

            logger.Trace("cmdResult.Anime: {0}", getAnimeCmd.Anime);

            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID) ?? new SVR_AniDB_Anime();
            if (!anime.PopulateAndSaveFromHTTP(session, getAnimeCmd.Anime, getAnimeCmd.Episodes, getAnimeCmd.Titles, getAnimeCmd.Tags,
                getAnimeCmd.Characters, getAnimeCmd.Staff, getAnimeCmd.Resources, getAnimeCmd.Relations, getAnimeCmd.SimilarAnime, getAnimeCmd.Recommendations,
                downloadRelations, relDepth, createSeriesEntry))
            {
                logger.Error($"Failed populate anime info for {animeID}");
                return null;
            }

            // All images from AniDB are downloaded in this
            if (validateImages)
            {
                var cmd = new CommandRequest_DownloadAniDBImages(anime.AnimeID, false);
                cmd.Save();
            }

            var series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
            // conditionally create AnimeSeries if it doesn't exist
            if (series == null && createSeriesEntry) {
                series = anime.CreateAnimeSeriesAndGroup(sessionWrapper);
            }
            // create AnimeEpisode records for all episodes in this anime only if we have a series
            if (series != null)
            {
                series.CreateAnimeEpisodes(session, anime);
                RepoFactory.AnimeSeries.Save(series, true, false);
            }
            SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);

            return anime;
        }

        public bool ValidAniDBCredentials()
        {
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password) ||
                string.IsNullOrEmpty(serverName)
                || string.IsNullOrEmpty(serverPort) || string.IsNullOrEmpty(clientPort))
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
            ServicePointManager.Expect100Continue = false;

            try
            {
                IPHostEntry localHostEntry = Dns.GetHostEntry(Dns.GetHostName());


                logger.Info("-------- Local IP Addresses --------");
                localIpEndPoint = new IPEndPoint(IPAddress.Any, Convert.ToInt32(clientPort));
                logger.Info("-------- End Local IP Addresses --------");

                soUdp.Bind(localIpEndPoint);
                soUdp.ReceiveTimeout = 30000; // 30 seconds

                logger.Info("BindToLocalPort: Bound to local address: {0} - Port: {1} ({2})",
                    localIpEndPoint,
                    clientPort,
                    localIpEndPoint.AddressFamily);

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Could not bind to local port: {ex}");
                return false;
            }
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

                logger.Info("BindToRemotePort: Bound to remote address: " + remoteIpEndPoint.Address +
                            " : " +
                            remoteIpEndPoint.Port);

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
