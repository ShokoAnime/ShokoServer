using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;
using ICSharpCode.SharpZipLib.Zip.Compression;
using NLog;
using Shoko.Commons.Properties;
using Shoko.Models.Enums;
using Shoko.Server.AniDB_API;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Requests;
using Shoko.Server.Providers.AniDB.UDP.Responses;
using Shoko.Server.Settings;
using Timer = System.Timers.Timer;

namespace Shoko.Server.Providers.AniDB
{
    public class AniDBConnectionHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static AniDBConnectionHandler _instance;

        public static AniDBConnectionHandler Instance => _instance ?? (_instance = new AniDBConnectionHandler());

        private static readonly object ConnectionLock = new object();

        private static readonly int HTTPBanTimerResetLength = 12;

        private static readonly int UDPBanTimerResetLength = 12;

        private IPEndPoint _localIpEndPoint;
        private IPEndPoint _remoteIpEndPoint;
        private Socket _aniDBSocket;
        public string SessionID { get; private set; }

        private readonly string _serverHost;
        private readonly ushort _serverPort;
        private readonly ushort _clientPort;

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
                    if (_httpBanResetTimer.Enabled)
                    {
                        Logger.Warn("HTTP ban timer was already running, ban time extending");
                        _httpBanResetTimer.Stop(); //re-start implies stop
                    }

                    _httpBanResetTimer.Start();
                    AniDBStateUpdate?.Invoke(this, new AniDBStateUpdate
                    {
                        Value = true,
                        UpdateType = AniDBUpdateType.HTTPBan,
                        UpdateTime = DateTime.Now,
                        PauseTimeSecs = TimeSpan.FromHours(HTTPBanTimerResetLength).Seconds
                    });
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

                    AniDBStateUpdate?.Invoke(this, new AniDBStateUpdate
                    {
                        Value = false,
                        UpdateType = AniDBUpdateType.HTTPBan,
                        UpdateTime = DateTime.Now,
                    });
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
                    AniDBStateUpdate?.Invoke(this, new AniDBStateUpdate
                    {
                        Value = true,
                        UpdateType = AniDBUpdateType.UDPBan,
                        UpdateTime = DateTime.Now,
                        PauseTimeSecs = TimeSpan.FromHours(UDPBanTimerResetLength).Seconds
                    });
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

                    AniDBStateUpdate?.Invoke(this, new AniDBStateUpdate
                    {
                        Value = false,
                        UpdateType = AniDBUpdateType.UDPBan,
                        UpdateTime = DateTime.Now
                    });
                }
            }
        }

        private bool _isInvalidSession;

        public bool IsInvalidSession
        {
            get => _isInvalidSession;

            set
            {
                _isInvalidSession = value;
                AniDBStateUpdate?.Invoke(this, new AniDBStateUpdate
                {
                    UpdateType = AniDBUpdateType.Invalid_Session,
                    UpdateTime = DateTime.Now,
                    Value = value
                });
            }
        }

        private bool _isLoggedOn;

        public bool IsLoggedOn
        {
            get => _isLoggedOn;
            set => _isLoggedOn = value;
        }

        private bool _waitingOnResponse;

        public bool WaitingOnResponse
        {
            get => _waitingOnResponse;
            set
            {
                _waitingOnResponse = value;
                WaitingOnResponseTime = value ? DateTime.Now : (DateTime?) null;
                AniDBStateUpdate?.Invoke(this, new AniDBStateUpdate
                {
                    UpdateType = AniDBUpdateType.WaitingOnResponse,
                    Value = value,
                    UpdateTime = DateTime.Now
                });
            }
        }

        public DateTime? WaitingOnResponseTime { get; set; }

        public int? ExtendPauseSecs { get; set; }

        public bool IsNetworkAvailable { private set; get; }

        private DateTime LastAniDBPing { get; set; } = DateTime.MinValue;

        private DateTime LastAniDBMessageNonPing { get; set; } = DateTime.MinValue;

        private DateTime LastMessage =>
            LastAniDBMessageNonPing < LastAniDBPing ? LastAniDBPing : LastAniDBMessageNonPing;

        public event EventHandler LoginFailed;
        public event EventHandler<AniDBStateUpdate> AniDBStateUpdate;

        public AniDBConnectionHandler(string serverHost, ushort serverPort, ushort clientPort)
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
            _aniDBSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            _isLoggedOn = false;

            if (!BindToLocalPort()) IsNetworkAvailable = false;
            if (!BindToRemotePort()) IsNetworkAvailable = false;

            _logoutTimer = new Timer {Interval = 5000, Enabled = true, AutoReset = true};
            _logoutTimer.Elapsed += LogoutTimer_Elapsed;

            Logger.Info("starting logout timer...");
            _logoutTimer.Start();

            _httpBanResetTimer = new Timer
            {
                AutoReset = false, Interval = TimeSpan.FromHours(HTTPBanTimerResetLength).TotalMilliseconds
            };
            _httpBanResetTimer.Elapsed += HTTPBanResetTimerElapsed;


            _udpBanResetTimer = new Timer
            {
                AutoReset = false, Interval = TimeSpan.FromHours(UDPBanTimerResetLength).TotalMilliseconds
            };
            _udpBanResetTimer.Elapsed += UDPBanResetTimerElapsed;
        }

        private void CloseConnections()
        {
            _logoutTimer?.Stop();
            _logoutTimer = null;
            if (_aniDBSocket == null) return;
            Logger.Info("Disposing...");
            try
            {
                _aniDBSocket.Shutdown(SocketShutdown.Both);
                if (_aniDBSocket.Connected)
                {
                    _aniDBSocket.Disconnect(false);
                }
            }
            catch (SocketException ex)
            {
                Logger.Error($"Failed to Shutdown and Disconnect the connection to AniDB: {ex}");
            }
            finally
            {
                Logger.Info("Closing AniDB Connection...");
                _aniDBSocket.Close();
                Logger.Info("Closed AniDB Connection");
                _aniDBSocket = null;
            }
        }

        void LogoutTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            TimeSpan tsAniDBUDPTemp = DateTime.Now - LastMessage;
            if (ExtendPauseSecs.HasValue && tsAniDBUDPTemp.TotalSeconds >= ExtendPauseSecs.Value)
                ResetBanTimer();

            if (!_isLoggedOn) return;

            // don't ping when anidb is taking a long time to respond
            if (WaitingOnResponse)
            {
                try
                {
                    if (WaitingOnResponseTime.HasValue)
                    {
                        Thread.CurrentThread.CurrentUICulture =
                            CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture);

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
                TimeSpan tsAniDBNonPing = DateTime.Now - LastAniDBMessageNonPing;
                TimeSpan tsPing = DateTime.Now - LastAniDBPing;
                TimeSpan tsAniDBUDP = DateTime.Now - LastMessage;

                // if we haven't sent a command for 45 seconds, send a ping just to keep the connection alive
                if (tsAniDBUDP.TotalSeconds >= Constants.PingFrequency &&
                    tsPing.TotalSeconds >= Constants.PingFrequency &&
                    !IsUdpBanned && !ExtendPauseSecs.HasValue)
                {
                    AniDBUDP_RequestPing ping = new AniDBUDP_RequestPing();
                    ping.Execute(this);
                }

                // TODO Make this update in the UI, rather than here
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
            Logger.Info($"HTTP ban ({HTTPBanTimerResetLength}h) is over");
            IsHttpBanned = false;
        }

        private void UDPBanResetTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Logger.Info($"UDP ban ({UDPBanTimerResetLength}h) is over");
            IsUdpBanned = false;
        }

        public void ExtendBanTimer(int secsToPause, string pauseReason)
        {
            // This Handles the Waiting Period For When AniDB is under heavy load. Not likely to be used
            ExtendPauseSecs = secsToPause;
            AniDBStateUpdate?.Invoke(this, new AniDBStateUpdate
            {
                UpdateType = AniDBUpdateType.Overload_Backoff,
                Value = true,
                UpdateTime = DateTime.Now,
                PauseTimeSecs = secsToPause,
                Message = pauseReason
            });
        }

        public void ResetBanTimer()
        {
            // This Handles the Waiting Period For When AniDB is under heavy load. Not likely to be used
            ExtendPauseSecs = null;
            AniDBStateUpdate?.Invoke(this, new AniDBStateUpdate
            {
                UpdateType = AniDBUpdateType.Overload_Backoff,
                Value = false,
                UpdateTime = DateTime.Now
            });
        }

        public bool Login(string userName, string password)
        {
            // check if we are already logged in
            if (_isLoggedOn) return true;

            if (!ValidAniDBCredentials(userName, password))
            {
                LoginFailed?.Invoke(this, null);
                return false;
            }

            AniDBUDP_Response<AniDBUDP_ResponseLogin> response;
            try
            {
                AniDBUDP_RequestLogin login = new AniDBUDP_RequestLogin
                {
                    Username = userName, Password = password, UseUnicode = true
                };
                // Never give Execute a null SessionID, except here
                response = login.Execute(this);
            }
            catch (Exception e)
            {
                Logger.Error($"Unable to login to AniDB: {e}");
                response = new AniDBUDP_Response<AniDBUDP_ResponseLogin>();
            }

            switch (response.Code)
            {
                case AniDBUDPReturnCode.LOGIN_FAILED:
                    IsInvalidSession = true;
                    IsLoggedOn = false;
                    Logger.Error("AniDB Login Failed: invalid credentials");
                    LoginFailed?.Invoke(this, null);
                    break;
                case AniDBUDPReturnCode.LOGIN_ACCEPTED:
                    SessionID = response.Response.SessionID;
                    _isLoggedOn = true;
                    IsInvalidSession = false;
                    return true;
                default:
                    IsLoggedOn = false;
                    IsInvalidSession = true;
                    break;
            }

            return false;
        }

        /// <summary>
        /// Actually get data from AniDB
        /// </summary>
        /// <param name="command">The request to be made (AUTH user=baka&amp;pass....)</param>
        /// <param name="needsUnicode">Only for Login, specify whether to ask for UTF16</param>
        /// <param name="disableLogging">Some commands have sensitive data</param>
        /// <param name="isPing">is it a ping command</param>
        /// <returns></returns>
        public AniDBUDP_Response<string> CallAniDBUDP(string command, bool needsUnicode = false,
            bool disableLogging = false, bool isPing = false)
        {
            // Steps:
            // 1. Check Ban state and throw if Banned
            // 2. Check Login State and Login if needed
            // 3. Actually Call AniDB

            // Check Ban State
            // Ideally, this will never happen, as we stop the queue and attempt a graceful rollback of the command
            if (IsUdpBanned) throw new UnexpectedAniDBResponseException {ReturnCode = AniDBUDPReturnCode.BANNED};
            // TODO Low Priority: We need to handle Login Attempt Decay, so that we can try again if it's not just a bad user/pass
            // It wasn't handled before, and it's not caused serious problems
            if (IsInvalidSession) throw new NotLoggedInException();

            // Check Login State
            if (!Login(ServerSettings.Instance.AniDb.Username, ServerSettings.Instance.AniDb.Password))
                throw new NotLoggedInException();

            // Actually Call AniDB
            return CallAniDBUDPDirectly(command, needsUnicode, disableLogging, isPing);
        }

        public AniDBUDP_Response<string> CallAniDBUDPDirectly(string command, bool needsUnicode, bool disableLogging,
            bool isPing)
        {
            // 1. Call AniDB
            // 2. Decode the response, converting Unicode and decompressing, as needed
            // 3. Check for an Error Response
            // 4. Return a pretty response object, with a parsed return code and trimmed string
            EndPoint remotePoint = _remoteIpEndPoint;
            Encoding encoding = Encoding.ASCII;
            if (needsUnicode) encoding = new UnicodeEncoding(true, false);

            AniDBRateLimiter.Instance.EnsureRate();
            DateTime start = DateTime.Now;

            if (!disableLogging)
            {
                string msg = $"AniDB UDP Call: (Using {(needsUnicode ? "Unicode" : "ASCII")}) {command}";
                ShokoService.LogToSystem(Constants.DBLogType.APIAniDBUDP, msg);
            }

            int received;
            byte[] byReceivedAdd = new byte[1600]; // max length should actually be 1400
            byte[] sendByteAdd = encoding.GetBytes(command.ToCharArray());
            try
            {
                StampLastMessage(isPing);
                WaitingOnResponse = true;
                _aniDBSocket.SendTo(sendByteAdd, _remoteIpEndPoint);
                received = _aniDBSocket.ReceiveFrom(byReceivedAdd, ref remotePoint);
                WaitingOnResponse = false;
                StampLastMessage(isPing);

                if ((received > 2) && (byReceivedAdd[0] == 0) && (byReceivedAdd[1] == 0))
                {
                    //deflate
                    byte[] buff = new byte[65536];
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

            Encoding receivedEncoding = GetEncoding(byReceivedAdd);

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
            if (decodedParts.Length < 2) throw new UnexpectedAniDBResponseException {Response = decodedString};

            if (truncated)
            {
                TimeSpan ts = DateTime.Now - start;
                string msg = decodedParts.Length > 0
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
            if (firstLineParts.Length != 2) throw new UnexpectedAniDBResponseException {Response = decodedString};

            // Can't parse the code
            if (!int.TryParse(firstLineParts[0], out int code))
                throw new UnexpectedAniDBResponseException {Response = decodedString};

            AniDBUDPReturnCode status = (AniDBUDPReturnCode) code;

            // if we get banned pause the command processor for a while
            // so we don't make the ban worse
            IsUdpBanned = status == AniDBUDPReturnCode.BANNED;

            switch (status)
            {
                // 598 UNKNOWN COMMAND usually means we had connections issue
                // 506 INVALID SESSION
                // 505 ILLEGAL INPUT OR ACCESS DENIED
                // reset login status to start again
                case AniDBUDPReturnCode.INVALID_SESSION:
                case AniDBUDPReturnCode.ILLEGAL_INPUT_OR_ACCESS_DENIED:
                case AniDBUDPReturnCode.UNKNOWN_COMMAND:
                    IsInvalidSession = true;
                    Logger.Trace("FORCING Logout because of invalid session");
                    ForceReconnection();
                    break;
                // 600 INTERNAL SERVER ERROR
                // 601 ANIDB OUT OF SERVICE - TRY AGAIN LATER
                // 602 SERVER BUSY - TRY AGAIN LATER
                // 604 TIMEOUT - DELAY AND RESUBMIT
                case AniDBUDPReturnCode.INTERNAL_SERVER_ERROR:
                case AniDBUDPReturnCode.ANIDB_OUT_OF_SERVICE:
                case AniDBUDPReturnCode.SERVER_BUSY:
                case AniDBUDPReturnCode.TIMEOUT_DELAY_AND_RESUBMIT:
                {
                    var errorMessage = $"{(int) status} {status}";

                    Logger.Trace("FORCING Logout because of invalid session");
                    ExtendBanTimer(300, errorMessage);
                    break;
                }
            }

            return new AniDBUDP_Response<string> {Code = status, Response = decodedParts[1].Trim()};
        }
        
        public static void ForceReconnection()
        {
            try
            {
                if (_instance != null)
                {
                    Logger.Info("Forcing reconnection to AniDB");
                    string serverHost = _instance._serverHost;
                    ushort serverPort = _instance._serverPort;
                    ushort clientPort = _instance._clientPort;
                    _instance.CloseConnections();
                    _instance = null;
                    AniDBRateLimiter.Instance.EnsureRate();

                    Init(serverHost, serverPort, clientPort);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.ToString());
            }
        }

        private void StampLastMessage(bool isPing)
        {
            if (isPing)
                LastAniDBPing = DateTime.Now;
            else
                LastAniDBMessageNonPing = DateTime.Now;
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
            if (!_isLoggedOn) return;
            AniDBUDP_RequestLogout req = new AniDBUDP_RequestLogout();
            req.Execute(this);
            _isLoggedOn = false;
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
            _localIpEndPoint = null;

            // Dont send Expect 100 requests. These requests aren't always supported by remote internet devices, in which case can cause failure.
            ServicePointManager.Expect100Continue = false;

            try
            {
                _localIpEndPoint = new IPEndPoint(IPAddress.Any, _clientPort);

                _aniDBSocket.Bind(_localIpEndPoint);
                _aniDBSocket.ReceiveTimeout = 30000; // 30 seconds

                Logger.Info("BindToLocalPort: Bound to local address: {0} - Port: {1} ({2})", _localIpEndPoint,
                    _clientPort, _localIpEndPoint.AddressFamily);

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
            _remoteIpEndPoint = null;

            try
            {
                IPHostEntry remoteHostEntry = Dns.GetHostEntry(_serverHost);
                _remoteIpEndPoint = new IPEndPoint(remoteHostEntry.AddressList[0], _serverPort);

                Logger.Info("BindToRemotePort: Bound to remote address: " + _remoteIpEndPoint.Address +
                            " : " +
                            _remoteIpEndPoint.Port);

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
