using System;
using System.Timers;
using NLog;
using Shoko.Models.Enums;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Providers.AniDB
{
    public class AniDBHttpConnectionHandler
    {
        private const int HTTPBanTimerResetLength = 12;
        
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
        public DateTime? HttpBanTime { get; set; }
        private bool _isHttpBanned;
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
                    State = new AniDBStateUpdate
                    {
                        Value = true,
                        UpdateType = AniDBUpdateType.HTTPBan,
                        UpdateTime = DateTime.Now,
                        PauseTimeSecs = TimeSpan.FromHours(HTTPBanTimerResetLength).Seconds
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
                        UpdateType = AniDBUpdateType.HTTPBan,
                        UpdateTime = DateTime.Now,
                    };
                }
            }
        }

        private void InitInternal()
        {
            _httpBanResetTimer = new Timer
            {
                AutoReset = false, Interval = TimeSpan.FromHours(HTTPBanTimerResetLength).TotalMilliseconds
            };
            _httpBanResetTimer.Elapsed += HTTPBanResetTimerElapsed;
        }
        
        private void HTTPBanResetTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Logger.Info($"HTTP ban ({HTTPBanTimerResetLength}h) is over");
            IsHttpBanned = false;
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
    }
}
