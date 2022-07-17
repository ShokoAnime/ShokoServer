using System;
using System.Linq;
using System.Text;
using System.Timers;
using Microsoft.Extensions.Logging;
using Shoko.Server.Commands;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Connection;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Timer = System.Timers.Timer;

namespace Shoko.Server.Providers.AniDB.UDP
{
    public class AniDBUDPConnectionHandler : ConnectionHandler, IUDPConnectionHandler
    {
        IServiceProvider IUDPConnectionHandler.ServiceProvider => ServiceProvider;
        private readonly IAniDBSocketHandler _socketHandler;

        public event EventHandler LoginFailed;

        public override int BanTimerResetLength => 12;
        public override string Type => "UDP";
        public override UpdateType BanEnum => UpdateType.UDPBan;

        public string SessionID { get; set; }
        private ServerSettings Settings { get; set; }

        private Timer _PulseTimer;

        private bool _isInvalidSession;
        public bool IsInvalidSession
        {
            get => _isInvalidSession;

            set
            {
                _isInvalidSession = value;
                UpdateState(new AniDBStateUpdate
                {
                    UpdateType = UpdateType.InvalidSession,
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

        public bool IsNetworkAvailable { private set; get; }

        private DateTime LastAniDBPing { get; set; } = DateTime.MinValue;

        private DateTime LastAniDBMessageNonPing { get; set; } = DateTime.MinValue;

        private DateTime LastMessage =>
            LastAniDBMessageNonPing < LastAniDBPing ? LastAniDBPing : LastAniDBMessageNonPing;

        public AniDBUDPConnectionHandler(IServiceProvider provider, CommandProcessor queue, AniDBSocketHandler socketHandler, ServerSettings settings, UDPRateLimiter rateLimiter) : base(provider, queue, rateLimiter)
        {
            _socketHandler = socketHandler;
            Settings = settings;
            InitInternal();
        }

        ~AniDBUDPConnectionHandler()
        {
            CloseConnections();
        }

        private void InitInternal()
        {
            _isLoggedOn = false;

            IsNetworkAvailable = _socketHandler.TryConnection();

            _PulseTimer = new Timer {Interval = 5000, Enabled = true, AutoReset = true};
            _PulseTimer.Elapsed += PulseTimerElapsed;

            Logger.LogInformation("starting logout timer...");
            _PulseTimer.Start();
        }

        private void CloseConnections()
        {
            _PulseTimer?.Stop();
            _PulseTimer = null;
            if (_socketHandler == null) return;
            Logger.LogInformation("AniDB UDP Socket Disposing...");
            _socketHandler.Dispose();
        }

        void PulseTimerElapsed(object sender, ElapsedEventArgs e)
        {
            TimeSpan tsAniDBUDPTemp = DateTime.Now - LastMessage;
            if (ExtendPauseSecs.HasValue && tsAniDBUDPTemp.TotalSeconds >= ExtendPauseSecs.Value)
                ResetBanTimer();

            if (!_isLoggedOn) return;

            // don't ping when anidb is taking a long time to respond
            if (_socketHandler.IsLocked) return;

            TimeSpan tsAniDBNonPing = DateTime.Now - LastAniDBMessageNonPing;
            TimeSpan tsPing = DateTime.Now - LastAniDBPing;
            TimeSpan tsAniDBUDP = DateTime.Now - LastMessage;

            // if we haven't sent a command for 45 seconds, send a ping just to keep the connection alive
            if (tsAniDBUDP.TotalSeconds >= Constants.PingFrequency &&
                tsPing.TotalSeconds >= Constants.PingFrequency &&
                !IsBanned && !ExtendPauseSecs.HasValue)
            {
                RequestPing ping = new RequestPing();
                ping.Execute(this);
            }

            if (tsAniDBNonPing.TotalSeconds > Constants.ForceLogoutPeriod) // after 10 minutes
            {
                ForceLogout();
            }
        }

        public bool Login()
        {
            // check if we are already logged in
            if (_isLoggedOn) return true;

            if (!ValidAniDBCredentials())
            {
                LoginFailed?.Invoke(this, null);
                return false;
            }

            UDPBaseResponse<ResponseLogin> response;
            try
            {
                RequestLogin login = new RequestLogin
                {
                    Username = Settings.AniDb.Username, Password = Settings.AniDb.Password
                };
                // Never give Execute a null SessionID, except here
                response = login.Execute(this);
            }
            catch (Exception e)
            {
                Logger.LogError($"Unable to login to AniDB: {e}");
                response = new UDPBaseResponse<ResponseLogin>();
            }

            switch (response.Code)
            {
                case UDPReturnCode.LOGIN_FAILED:
                    IsInvalidSession = true;
                    IsLoggedOn = false;
                    Logger.LogError("AniDB Login Failed: invalid credentials");
                    LoginFailed?.Invoke(this, null);
                    break;
                case UDPReturnCode.LOGIN_ACCEPTED:
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
        public UDPBaseResponse<string> CallAniDBUDP(string command, bool needsUnicode = true,
            bool disableLogging = false, bool isPing = false)
        {
            // Steps:
            // 1. Check Ban state and throw if Banned
            // 2. Check Login State and Login if needed
            // 3. Actually Call AniDB

            // Check Ban State
            // Ideally, this will never happen, as we stop the queue and attempt a graceful rollback of the command
            if (IsBanned) throw new AniDBBannedException { BanType = UpdateType.UDPBan, BanExpires = BanTime?.AddHours(BanTimerResetLength) };
            // TODO Low Priority: We need to handle Login Attempt Decay, so that we can try again if it's not just a bad user/pass
            // It wasn't handled before, and it's not caused serious problems
            if (IsInvalidSession) throw new NotLoggedInException();

            // Check Login State
            if (!Login())
                throw new NotLoggedInException();

            // Actually Call AniDB
            return CallAniDBUDPDirectly(command, needsUnicode, disableLogging, isPing);
        }

        public UDPBaseResponse<string> CallAniDBUDPDirectly(string command, bool needsUnicode=true, bool disableLogging=false,
            bool isPing=false, bool returnFullResponse=false)
        {
            // 1. Call AniDB
            // 2. Decode the response, converting Unicode and decompressing, as needed
            // 3. Check for an Error Response
            // 4. Return a pretty response object, with a parsed return code and trimmed string
            Encoding encoding = Encoding.ASCII;
            if (needsUnicode) encoding = new UnicodeEncoding(true, false);

            RateLimiter.EnsureRate();
            DateTime start = DateTime.Now;

            if (!disableLogging)
            {
                string msg = $"AniDB UDP Call: (Using {(needsUnicode ? "Unicode" : "ASCII")}) {command}";
                ShokoService.LogToSystem(Constants.DBLogType.APIAniDBUDP, msg);
            }

            byte[] sendByteAdd = encoding.GetBytes(command);
            StampLastMessage(isPing);
            var byReceivedAdd = _socketHandler.Send(sendByteAdd);
            StampLastMessage(isPing);

            // decode
            string decodedString = GetEncoding(byReceivedAdd).GetString(byReceivedAdd, 0, byReceivedAdd.Length);
            if (decodedString[0] == 0xFEFF) // remove BOM
                decodedString = decodedString[1..];

            // there should be 2 newline characters in each response
            // the first is after the command .e.g "220 FILE"
            // the second is at the end of the data
            string[] decodedParts = decodedString.Split('\n');
            bool truncated = decodedString.Count(a => a == '\n') != 2;

            // If the parts don't have at least 2 items, then we don't have a valid response
            // parts[0] => 200 FILE
            // parts[1] => Response
            // parts[2] => empty, since we ended with a newline
            if (decodedParts.Length < 2) throw new UnexpectedUDPResponseException {Response = decodedString};

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

            string[] firstLineParts = decodedParts[0].Split(' ', 2);
            // If we don't have 2 parts of the first line, then it's not in the expected
            // 200 FILE
            // Format
            if (firstLineParts.Length != 2) throw new UnexpectedUDPResponseException {Response = decodedString};

            // Can't parse the code
            if (!int.TryParse(firstLineParts[0], out int code))
                throw new UnexpectedUDPResponseException {Response = decodedString};

            UDPReturnCode status = (UDPReturnCode) code;

            // if we get banned pause the command processor for a while
            // so we don't make the ban worse
            IsBanned = status == UDPReturnCode.BANNED;
            
            // if banned, then throw the ban exception. There will be no data in the response
            if (IsBanned) throw new AniDBBannedException { BanType = UpdateType.UDPBan, BanExpires = BanTime?.AddHours(BanTimerResetLength) };

            switch (status)
            {
                // 598 UNKNOWN COMMAND usually means we had connections issue
                // 506 INVALID SESSION
                // 505 ILLEGAL INPUT OR ACCESS DENIED
                // reset login status to start again
                case UDPReturnCode.INVALID_SESSION:
                case UDPReturnCode.ILLEGAL_INPUT_OR_ACCESS_DENIED:
                case UDPReturnCode.UNKNOWN_COMMAND:
                    IsInvalidSession = true;
                    Logger.LogTrace("FORCING Logout because of invalid session");
                    ForceReconnection();
                    break;
                // 600 INTERNAL SERVER ERROR
                // 601 ANIDB OUT OF SERVICE - TRY AGAIN LATER
                // 602 SERVER BUSY - TRY AGAIN LATER
                // 604 TIMEOUT - DELAY AND RESUBMIT
                case UDPReturnCode.INTERNAL_SERVER_ERROR:
                case UDPReturnCode.ANIDB_OUT_OF_SERVICE:
                case UDPReturnCode.SERVER_BUSY:
                case UDPReturnCode.TIMEOUT_DELAY_AND_RESUBMIT:
                {
                    var errorMessage = $"{(int) status} {status}";

                    Logger.LogTrace("FORCING Logout because of invalid session");
                    ExtendBanTimer(300, errorMessage);
                    break;
                }
            }

            if (returnFullResponse) return new UDPBaseResponse<string> {Code = status, Response = decodedString};
            return new UDPBaseResponse<string> {Code = status, Response = decodedParts[1].Trim()};
        }
        
        public void ForceReconnection()
        {
            try
            {
               ForceLogout(); 
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.ToString());
            }

            try
            {
                CloseConnections();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.ToString());
            }
            
            try
            {
                InitInternal();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.ToString());
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

        public void ForceLogout()
        {
            if (!_isLoggedOn) return;
            RequestLogout req = new RequestLogout();
            req.Execute(this);
            _isLoggedOn = false;
        }

        public bool SetCredentials(string username, string password)
        {
            if (!ValidAniDBCredentials(username, password)) return false;
            Settings.AniDb.Username = username;
            Settings.AniDb.Password = password;
            Settings.SaveSettings();
            return true;
        }

        public bool ValidAniDBCredentials(string user = null, string pass = null)
        {
            user ??= Settings.AniDb.Username;
            pass ??= Settings.AniDb.Password;
            if (string.IsNullOrEmpty(user)) return false;
            if (string.IsNullOrEmpty(pass)) return false;
            return true;
        }
    }
}
