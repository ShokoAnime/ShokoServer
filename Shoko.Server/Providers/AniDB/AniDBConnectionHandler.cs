using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;
using AniDBAPI;
using AniDBAPI.Commands;
using ICSharpCode.SharpZipLib.Zip.Compression;
using NLog;
using Shoko.Commons.Properties;
using Shoko.Server.AniDB_API;
using Shoko.Server.Providers.AniDB.MyList;
using Shoko.Server.Providers.AniDB.MyList.Exceptions;
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

        /// <summary>
        /// Actually get data from AniDB
        /// </summary>
        /// <param name="command">The request to be made (AUTH user=baka&amp;pass....)</param>
        /// <param name="needsUnicode"></param>
        /// <param name="disableLogging">Some commands have sensitive data</param>
        /// <returns></returns>
        public AniDBUDP_Response<string> CallAniDB(string command, bool needsUnicode = false, bool disableLogging = false)
        {
            // Steps:
            // 1. Check Login State and Login if needed
            // 2. Actually Call AniDB

            // Actually Call AniDB
            return CallAniDBDirectly(command, needsUnicode, disableLogging);
        }

        public AniDBUDP_Response<string> CallAniDBDirectly(string command, bool needsUnicode, bool disableLogging)
        {
            // 1. Call AniDB
            // 2. Decode the response, converting Unicode and decompressing, as needed
            // 3. Check for an Error Response
            // 4. Return a pretty response object, with a parsed return code and trimmed string
            EndPoint remotePoint = remoteIpEndPoint;
            Encoding encoding = Encoding.ASCII;
            if (needsUnicode) encoding = new UnicodeEncoding(true, false);

            AniDbRateLimiter.Instance.EnsureRate();

            DateTime start = DateTime.Now;

            if (!disableLogging)
            {
                string msg = $"AniDB UDP Call: (Using {(needsUnicode ? "Unicode" : "ASCII")}) {command}";
                ShokoService.LogToSystem(Constants.DBLogType.APIAniDBUDP, msg);
            }

            // TODO Maybe remove
            bool repeat;
            int received;
            Byte[] byReceivedAdd = new Byte[2000]; // max length should actually be 1400
            Encoding receivedEncoding;
            do
            {
                repeat = false;
                Byte[] sendByteAdd = encoding.GetBytes(command.ToCharArray());
                try
                {
                    // TODO Event for LastAniDBMessage
                    /*ShokoService.LastAniDBMessage = DateTime.Now;
                    ShokoService.LastAniDBUDPMessage = DateTime.Now;
                    if (commandType != enAniDBCommandType.Ping)
                        ShokoService.LastAniDBMessageNonPing = DateTime.Now;
                    else
                        ShokoService.LastAniDBPing = DateTime.Now;*/

                    // Send Request  
                    AniDBSocket.SendTo(sendByteAdd, remoteIpEndPoint);

                    // Receive Response
                    received = AniDBSocket.ReceiveFrom(byReceivedAdd, ref remotePoint);
                    // TODO Event for LastAniDBMessage
                    /*ShokoService.LastAniDBMessage = DateTime.Now;
                    ShokoService.LastAniDBUDPMessage = DateTime.Now;
                    if (commandType != enAniDBCommandType.Ping)
                        ShokoService.LastAniDBMessageNonPing = DateTime.Now;
                    else
                        ShokoService.LastAniDBPing = DateTime.Now;*/

                    //MyAnimeLog.Write("Buffer length = {0}", received.ToString());
                    if ((received > 2) && (byReceivedAdd[0] == 0) && (byReceivedAdd[1] == 0))
                    {
                        //deflate
                        Byte[] buff = new byte[65536];
                        Byte[] input = new byte[received - 2];
                        Array.Copy(byReceivedAdd, 2, input, 0, received - 2);
                        Inflater inf = new Inflater(false);
                        inf.SetInput(input);
                        inf.Inflate(buff);
                        byReceivedAdd = buff;
                        received = (int) inf.TotalOut;
                    }
                }
                catch (SocketException sex)
                {
                    // most likely we have timed out
                    Logger.Error(sex);
                    received = 0;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    received = 0;
                }

                // TODO Need a graceful way to handle this. Hopefully, I'll receive a message from AniDB guys about how to handle it
                receivedEncoding = GetEncoding(byReceivedAdd);
/*
                    if (commandType == Login && (receivedEncoding.EncodingName.ToLower().StartsWith("unicode") &&
                                                                    !encoding.EncodingName.ToLower().StartsWith("unicode")))
                    {
                        //Previous Session used utf-16 and was not logged out, AniDB was not yet issued a timeout.
                        //AUTH command was not understand because it was encoded in ASCII.
                        repeatcmd = true;
                    }
*/
            } while (repeat);

            // decode
            string decodedString = receivedEncoding.GetString(byReceivedAdd, 0, received);
            byte[] bom = GetBOM(receivedEncoding);
            decodedString = decodedString.Substring(bom.Length);

            // there should be 2 newline characters in each response
            // the first is after the command .e.g "220 FILE"
            // the second is at the end of the data
            string[] decodedParts = decodedString.Split('\n');
            bool truncated = decodedString.Count(a => a == '\n') != 2;
            
            // If the parts don't have at least 2 items, then we don't have a valid response
            // parts[0] => 200 FILE
            // parts[1] => Response
            // parts[2] => empty, since we ended with a newline
            if (decodedParts.Length < 2) throw new UnexpectedAniDBResponse {Response = decodedString};

            if (truncated)
            {
                TimeSpan ts = DateTime.Now - start;
                string msg;
                msg = decodedParts.Length > 0
                    ? $"UDP_RESPONSE_TRUNC in {ts.TotalMilliseconds}ms - {decodedParts[1]}"
                    : $"UDP_RESPONSE_TRUNC in {ts.TotalMilliseconds}ms - {decodedString}";
                ShokoService.LogToSystem(Constants.DBLogType.APIAniDBUDP, msg);
            }
            else
            {
                TimeSpan ts = DateTime.Now - start;
                string msg = $"UDP_RESPONSE in {ts.TotalMilliseconds} ms - {decodedParts} ";
                ShokoService.LogToSystem(Constants.DBLogType.APIAniDBUDP, msg);
            }

            string[] firstLineParts = decodedParts[0].Split(' ');
            // If we don't have 2 parts of the first line, then it's not in the expected
            // 200 FILE
            // Format
            if (firstLineParts.Length != 2) throw new UnexpectedAniDBResponse {Response = decodedString};

            // Can't parse the code
            if (!int.TryParse(firstLineParts[0], out int code)) throw new UnexpectedAniDBResponse {Response = decodedString};
            
            // if we get banned pause the command processor for a while
            // so we don't make the ban worse
            IsUdpBanned = code == 555;

            // TODO Ban Event for these
            // 598 UNKNOWN COMMAND usually means we had connections issue
            // 506 INVALID SESSION
            // 505 ILLEGAL INPUT OR ACCESS DENIED
            // reset login status to start again
            if (code == 598 || code == 506 || code == 505)
            {
                IsInvalidSession = true;
                Logger.Trace("FORCING Logout because of invalid session");
                //ForceReconnection();
            }

            // 600 INTERNAL SERVER ERROR
            // 601 ANIDB OUT OF SERVICE - TRY AGAIN LATER
            // 602 SERVER BUSY - TRY AGAIN LATER
            // 604 TIMEOUT - DELAY AND RESUBMIT
            if (code == 600 || code == 601 || code == 602 || code == 604)
            {
                string errormsg = string.Empty;
                switch (code)
                {
                    case 600:
                        errormsg = "600 INTERNAL SERVER ERROR";
                        break;
                    case 601:
                        errormsg = "601 ANIDB OUT OF SERVICE - TRY AGAIN LATER";
                        break;
                    case 602:
                        errormsg = "602 SERVER BUSY - TRY AGAIN LATER";
                        break;
                    case 604:
                        errormsg = "TIMEOUT - DELAY AND RESUBMIT";
                        break;
                }

                Logger.Trace("FORCING Logout because of invalid session");
                ExtendBanTimer(300, errormsg);
            }
            
            return new AniDBUDP_Response<string> {Code = (AniDBUDPReturnCode) code, Response = decodedParts[1].Trim()};
        }
        
        /// <summary>
        /// Determines an encoded string's encoding by analyzing its byte order mark (BOM).
        /// Defaults to ASCII when detection of the text file's endianness fails.
        /// </summary>
        /// <param name="data">Byte array of the encoded string</param>
        /// <returns>The detected encoding.</returns>
        public static Encoding GetEncoding(byte[] data)
        {
            if (data.Length < 4) return Encoding.ASCII;
            // Analyze the BOM
            if (data[0] == 0x2b && data[1] == 0x2f && data[2] == 0x76) return Encoding.UTF7;
            if (data[0] == 0xef && data[1] == 0xbb && data[2] == 0xbf) return Encoding.UTF8;
            if (data[0] == 0xff && data[1] == 0xfe) return Encoding.Unicode; //UTF-16LE
            if (data[0] == 0xfe && data[1] == 0xff) return Encoding.BigEndianUnicode; //UTF-16BE
            if (data[0] == 0 && data[1] == 0 && data[2] == 0xfe && data[3] == 0xff) return Encoding.UTF32;
            return Encoding.ASCII;
        }

        public static byte[] GetBOM(Encoding enc)
        {
            if (enc.Equals(Encoding.UTF7)) return new byte[] {0x2b, 0x2f, 0x76};
            if (enc.Equals(Encoding.UTF8)) return new byte[] {0xef, 0xbb, 0xbf};
            if (enc.Equals(Encoding.Unicode)) return new byte[] {0xff, 0xfe};
            if (enc.Equals(Encoding.BigEndianUnicode)) return new byte[] {0xfe, 0xff};
            if (enc.Equals(Encoding.UTF32)) return new byte[] {0x0, 0x0, 0xfe, 0xff};
            return new byte[0];
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