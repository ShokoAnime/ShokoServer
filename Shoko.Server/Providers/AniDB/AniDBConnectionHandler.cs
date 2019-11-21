using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;
using AniDBAPI;
using AniDBAPI.Commands;
using NLog;
using Shoko.Commons.Properties;
using Shoko.Server.Settings;
using Timer = System.Timers.Timer;

namespace Shoko.Server.Providers.AniDB
{
    public class AniDBConnectionHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static AniDBConnectionHandler _instance { get; set; }

        public static AniDBConnectionHandler Instance => _instance ?? (_instance = new AniDBConnectionHandler());
        
        private readonly object ConnectionLock = new object();
        
        private static readonly int HTTPBanTimerResetLength = 12;
        
        private static readonly int UDPBanTimerResetLength = 12;

        private IPEndPoint localIpEndPoint;
        private IPEndPoint remoteIpEndPoint;
        public Socket AniDBSocket;
        private string _sessionString;

        private string _serverHost;
        private int _serverPort;
        private int _clientPort;

        private Timer _logoutTimer;

        private Timer _httpBanResetTimer;
        private Timer _udpBanResetTimer;
        
        public DateTime? HttpBanTime { get; set; }
        public DateTime? UdpBanTime { get; set; }

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
                    if (_httpBanResetTimer.Enabled)
                    {
                        Logger.Warn("HTTP ban timer was already running, ban time extending");
                        _httpBanResetTimer.Stop(); //re-start implies stop
                    }
                    _httpBanResetTimer.Start();
                    Analytics.PostEvent("AniDB", "Http Banned");
                }
                else
                {
                    if (_httpBanResetTimer.Enabled)
                    {
                        _httpBanResetTimer.Stop();
                        Logger.Info("HTTP ban timer stopped. Resuming queue if not paused.");
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
                    if (_udpBanResetTimer.Enabled)
                    {
                        Logger.Warn("UDP ban timer was already running, ban time extending");
                        _udpBanResetTimer.Stop(); // re-start implies stop
                    }
                    _udpBanResetTimer.Start();
                    Analytics.PostEvent("AniDB", "Udp Banned");
                }
                else
                {
                    if (_udpBanResetTimer.Enabled)
                    {
                        _udpBanResetTimer.Stop();
                        Logger.Info("UDP ban timer stopped. Resuming if not Paused");
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
            }
        }

        private bool isLoggedOn;

        public bool IsLoggedOn
        {
            get => isLoggedOn;
            set => isLoggedOn = value;
        }
        
        private bool _waitingOnResponse { get; set; }

        public bool WaitingOnResponse
        {
            get => _waitingOnResponse;
            set
            {
                _waitingOnResponse = value;
                ServerInfo.Instance.WaitingOnResponseAniDBUDP = value;

                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);

                if (value)
                {
                    ServerInfo.Instance.WaitingOnResponseAniDBUDPString =
                        Resources.AniDB_ResponseWait;
                    WaitingOnResponseTime = DateTime.Now;
                }
                else
                {
                    ServerInfo.Instance.WaitingOnResponseAniDBUDPString = Resources.Command_Idle;
                    WaitingOnResponseTime = null;
                }
            }
        }

        public DateTime? WaitingOnResponseTime { get; set; }

        public int? ExtendPauseSecs { get; set; }

        public bool IsNetworkAvailable { private set; get; }

        public string ExtendPauseReason { get; set; } = string.Empty;

        public event EventHandler LoginFailed;
        public event EventHandler<BannedEventArgs> Banned;
        
        public AniDBConnectionHandler(string serverHost, int serverPort, int clientPort)
        {
            _serverHost = serverHost;
            _serverPort = serverPort;
            _clientPort = clientPort;
        }

        private AniDBConnectionHandler() : this(ServerSettings.Instance.AniDb.ServerAddress,
            ServerSettings.Instance.AniDb.ServerPort, ServerSettings.Instance.AniDb.ClientPort)
        {
        }

        ~AniDBConnectionHandler()
        {
            CloseConnections();
        }
        
        public static void Init(string serverName, ushort serverPort, ushort clientPort)
        {
            _instance = new AniDBConnectionHandler(serverName, serverPort, clientPort);
            _instance.InitInternal();
        }

        private void InitInternal()
        {
            AniDBSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            
            isLoggedOn = false;

            if (!BindToLocalPort()) IsNetworkAvailable = false;
            if (!BindToRemotePort()) IsNetworkAvailable = false;

            _logoutTimer = new Timer();
            _logoutTimer.Elapsed += LogoutTimer_Elapsed;
            _logoutTimer.Interval = 5000; // Set the Interval to 5 seconds.
            _logoutTimer.Enabled = true;
            _logoutTimer.AutoReset = true;

            Logger.Info("starting logout timer...");
            _logoutTimer.Start();

            _httpBanResetTimer = new Timer();
            _httpBanResetTimer.AutoReset = false;
            _httpBanResetTimer.Elapsed += HTTPBanResetTimerElapsed;
            _httpBanResetTimer.Interval = TimeSpan.FromHours(HTTPBanTimerResetLength).TotalMilliseconds;

            _udpBanResetTimer = new Timer();
            _udpBanResetTimer.AutoReset = false;
            _udpBanResetTimer.Elapsed += UDPBanResetTimerElapsed;
            _udpBanResetTimer.Interval = TimeSpan.FromHours(UDPBanTimerResetLength).TotalMilliseconds;
        }
        
        public void CloseConnections()
        {
            Logger.Info("Disposing...");
            _logoutTimer?.Stop();
            _logoutTimer = null;
            if (AniDBSocket == null) return;
            try {
                AniDBSocket.Shutdown(SocketShutdown.Both);
                if (AniDBSocket.Connected) {
                    AniDBSocket.Disconnect(false);
                }
            }
            catch (SocketException ex) {
                Logger.Error($"Failed to Shutdown and Disconnect the connection to AniDB: {ex}");
            }
            finally {
                Logger.Info("Closing AniDB Connection...");
                AniDBSocket.Close();
                Logger.Info("Closed AniDB Connection");
                AniDBSocket = null;
            }
        }

        void LogoutTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            TimeSpan tsAniDBUDPTemp = DateTime.Now - ShokoService.LastAniDBUDPMessage;
            if (ExtendPauseSecs.HasValue && tsAniDBUDPTemp.TotalSeconds >= ExtendPauseSecs.Value)
                ResetBanTimer();

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

            lock (ConnectionLock)
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
                    ping.Process(ref AniDBSocket, ref remoteIpEndPoint, _sessionString, new UnicodeEncoding(true, false));
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
            Logger.Info("HTTP ban (12h) is over");
            IsHttpBanned = false;
        }

        private void UDPBanResetTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Logger.Info("UDP ban (12h) is over");
            IsUdpBanned = false;
        }

        public void ExtendBanTimer(int secsToPause, string pauseReason)
        {
            // TODO Move this to a subscribed event, so as to not call unnecessary UI stuff from here
            // Banned.Invoke(this, new BannedEventArgs {Banned = true, TimeSecs = secsToPause, Reason = pauseReason});
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);

            ExtendPauseSecs = secsToPause;
            ExtendPauseReason = pauseReason;
            ServerInfo.Instance.ExtendedPauseString = string.Format(Resources.AniDB_Paused, secsToPause, pauseReason);
            ServerInfo.Instance.HasExtendedPause = true;
        }

        public void ResetBanTimer()
        {
            // TODO Move this to a subscribed event, so as to not call unnecessary UI stuff from here
            // Banned.Invoke(this, new BannedEventArgs {Banned = false});
            ExtendPauseSecs = null;
            ExtendPauseReason = string.Empty;
            ServerInfo.Instance.ExtendedPauseString = string.Empty;
            ServerInfo.Instance.HasExtendedPause = false;
        }
        
        public bool Login(string userName, string password)
        {
            // TODO move this to new system
            // check if we are already logged in
            if (isLoggedOn) return true;

            if (!ValidAniDBCredentials(userName, password)) return false;

            AniDBCommand_Login login = new AniDBCommand_Login();
            login.Init(userName, password);

            string msg = login.commandText.Replace(userName, "******");
            msg = msg.Replace(password, "******");
            Logger.Trace("udp command: {0}", msg);
            WaitingOnResponse = true;
            AniDBUDPResponseCode ev = login.Process(ref AniDBSocket, ref remoteIpEndPoint, _sessionString,
                new UnicodeEncoding(true, false));
            WaitingOnResponse = false;

            if (login.errorOccurred)
                Logger.Trace("error in login: {0}", login.errorMessage);

            Thread.Sleep(2200);

            switch (ev)
            {
                case AniDBUDPResponseCode.LoginFailed:
                    Logger.Error("AniDB Login Failed: invalid credentials");
                    LoginFailed?.Invoke(this, null);
                    break;
                case AniDBUDPResponseCode.LoggedIn:
                    _sessionString = login.SessionID;
                    isLoggedOn = true;
                    IsInvalidSession = false;
                    return true;
                default:
                    Logger.Error($"AniDB Login Failed: error connecting to AniDB: {login.errorMessage}");
                    break;
            }

            return false;
        }

        public void ForceLogout()
        {
            // TODO Move this to new system
            if (!isLoggedOn) return;
            AniDBCommand_Logout logout = new AniDBCommand_Logout();
            logout.Init();
            WaitingOnResponse = true;
            logout.Process(ref AniDBSocket, ref remoteIpEndPoint, _sessionString, new UnicodeEncoding(true, false));
            WaitingOnResponse = false;
            isLoggedOn = false;
        }
        
        public bool ValidAniDBCredentials(string userName, string password)
        {
            if (string.IsNullOrEmpty(userName)) return false;
            if (string.IsNullOrEmpty(password)) return false;
            if (string.IsNullOrEmpty(_serverHost)) return false;
            if (_serverPort == 0) return false;
            if (_clientPort == 0) return false;
            return true;
        }

        private bool BindToLocalPort()
        {
            localIpEndPoint = null;

            // Dont send Expect 100 requests. These requests aren't always supported by remote internet devices, in which case can cause failure.
            ServicePointManager.Expect100Continue = false;

            try
            {
                localIpEndPoint = new IPEndPoint(IPAddress.Any, _clientPort);

                AniDBSocket.Bind(localIpEndPoint);
                AniDBSocket.ReceiveTimeout = 30000; // 30 seconds

                Logger.Info("BindToLocalPort: Bound to local address: {0} - Port: {1} ({2})", localIpEndPoint,
                    _clientPort, localIpEndPoint.AddressFamily);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Could not bind to local port: {ex}");
                return false;
            }
        }

        private bool BindToRemotePort()
        {
            remoteIpEndPoint = null;

            try
            {
                IPHostEntry remoteHostEntry = Dns.GetHostEntry(_serverHost);
                remoteIpEndPoint = new IPEndPoint(remoteHostEntry.AddressList[0], _serverPort);

                Logger.Info("BindToRemotePort: Bound to remote address: " + remoteIpEndPoint.Address +
                            " : " +
                            remoteIpEndPoint.Port);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Could not bind to remote port: {ex}");
                return false;
            }
        }
    }
}