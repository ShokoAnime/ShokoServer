using System;
using System.IO;
using System.Net;
using System.Text;
using System.Timers;
using NLog;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Providers.AniDB.Http
{
    public class AniDBHttpConnectionHandler
    {
        public const int BanTimerResetLength = 12;
        
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static AniDBHttpConnectionHandler _instance;
        public static AniDBHttpConnectionHandler Instance => _instance ??= new AniDBHttpConnectionHandler();

        public event EventHandler<AniDBStateUpdate> AniDBStateUpdate;

        private AniDBStateUpdate _currentState;
        public AniDBStateUpdate State
        {
            get => _currentState;
            set
            {
                if (value != _currentState)
                {
                    _currentState = value;
                    AniDBStateUpdate?.Invoke(this, _currentState);
                }
            }
        }
        
        public int? ExtendPauseSecs { get; set; }
        private Timer _httpBanResetTimer;
        public DateTime? BanTime { get; set; }
        private bool _isBanned;
        public bool IsBanned
        {
            get => _isBanned;
            set
            {
                _isBanned = value;
                if (value)
                {
                    BanTime = DateTime.Now;
                    if (_httpBanResetTimer.Enabled)
                    {
                        Logger.Warn("HTTP ban timer was already running, ban time extending");
                        _httpBanResetTimer.Stop(); //re-start implies stop
                    }

                    _httpBanResetTimer.Start();
                    State = new AniDBStateUpdate
                    {
                        Value = true,
                        UpdateType = UpdateType.HTTPBan,
                        UpdateTime = DateTime.Now,
                        PauseTimeSecs = TimeSpan.FromHours(BanTimerResetLength).Seconds
                    };
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

                    State = new AniDBStateUpdate
                    {
                        Value = false,
                        UpdateType = UpdateType.HTTPBan,
                        UpdateTime = DateTime.Now,
                    };
                }
            }
        }

        private void InitInternal()
        {
            _httpBanResetTimer = new Timer
            {
                AutoReset = false, Interval = TimeSpan.FromHours(BanTimerResetLength).TotalMilliseconds
            };
            _httpBanResetTimer.Elapsed += HTTPBanResetTimerElapsed;
        }
        
        private void HTTPBanResetTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Logger.Info($"HTTP ban ({BanTimerResetLength}h) is over");
            IsBanned = false;
        }
        
        public void ExtendBanTimer(int secsToPause, string pauseReason)
        {
            // This Handles the Waiting Period For When AniDB is under heavy load. Not likely to be used
            ExtendPauseSecs = secsToPause;
            AniDBStateUpdate?.Invoke(this, new AniDBStateUpdate
            {
                UpdateType = UpdateType.OverloadBackoff,
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
                UpdateType = UpdateType.OverloadBackoff,
                Value = false,
                UpdateTime = DateTime.Now
            });
        }
        
        public HttpBaseResponse<string> GetHttp(string url)
        {
            try
            {
                AniDBRateLimiter.UDP.EnsureRate();

                HttpWebRequest webReq = (HttpWebRequest) WebRequest.Create(url);
                webReq.Timeout = 20000; // 20 seconds
                webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                webReq.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:40.0) Gecko/20100101 Firefox/40.1";

                webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                using HttpWebResponse webResponse = (HttpWebResponse) webReq.GetResponse();
                if (webResponse.StatusCode == HttpStatusCode.OK && webResponse.ContentLength == 0)
                    throw new EndOfStreamException("Response Body was expected, but none returned");
                
                using Stream responseStream = webResponse.GetResponseStream();
                if (responseStream == null)
                    throw new EndOfStreamException("Response Body was expected, but none returned");

                string charset = webResponse.CharacterSet;
                Encoding encoding = null;
                if (!string.IsNullOrEmpty(charset))
                    encoding = Encoding.GetEncoding(charset);
                if (encoding == null)
                    encoding = Encoding.UTF8;
                StreamReader reader = new StreamReader(responseStream, encoding);

                string output = reader.ReadToEnd();

                if (CheckForBan(output)) return null;
                return new HttpBaseResponse<string> {Response = output, Code = webResponse.StatusCode};
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }
        
        public bool CheckForBan(string xmlresult)
        {
            if (string.IsNullOrEmpty(xmlresult)) return false;
            var index = xmlresult.IndexOf(@">banned<", StringComparison.InvariantCultureIgnoreCase);
            if (-1 >= index) return false;
            Logger.Warn("HTTP Banned!");
            IsBanned = true;
            return true;
        }
    }
}
