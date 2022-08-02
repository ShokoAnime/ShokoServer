using System;
using System.Timers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Server.Commands;
using Shoko.Server.Utilities;

namespace Shoko.Server.Providers.AniDB
{
    public abstract class ConnectionHandler
    {
        protected ILogger Logger { get; set; } 
        internal IServiceProvider ServiceProvider { get; set; }
        protected CommandProcessor GeneralQueue { get; set; }
        protected AniDBRateLimiter RateLimiter { get; set; }
        public abstract double BanTimerResetLength { get; }
        public abstract string Type { get; }
        public abstract UpdateType BanEnum { get; }
        
        public event EventHandler<AniDBStateUpdate> AniDBStateUpdate;
        protected AniDBStateUpdate _currentState;
        public AniDBStateUpdate State
        {
            get => _currentState;
            set
            {
                if (value != _currentState)
                {
                    _currentState = value;
                    UpdateState(_currentState);
                }
            }
        }

        protected int? ExtendPauseSecs { get; set; }
        private Timer BanResetTimer;
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
                    Logger.LogWarning($"AniDB {Type} Banned!");
                    if (BanResetTimer.Enabled)
                    {
                        Logger.LogWarning($"AniDB {Type} ban timer was already running, ban time extending");
                        BanResetTimer.Stop(); //re-start implies stop
                    }

                    BanResetTimer.Start();
                    State = new AniDBStateUpdate
                    {
                        Value = true,
                        UpdateType = BanEnum,
                        UpdateTime = DateTime.Now,
                        PauseTimeSecs = TimeSpan.FromHours(BanTimerResetLength).Seconds
                    };
                    Analytics.PostEvent("AniDB", "Http Banned");
                }
                else
                {
                    if (BanResetTimer.Enabled)
                    {
                        BanResetTimer.Stop();
                        Logger.LogInformation($"AniDB {Type} ban timer stopped. Resuming queue if not paused.");
                        // Skip if paused
                        if (!GeneralQueue.Paused)
                        {
                            // Needs to have something to do first
                            if (GeneralQueue.QueueCount > 0)
                            {
                                // Not really a new command, but this will start the queue if it's not running,
                                // with handling for problems
                                GeneralQueue.NotifyOfNewCommand();
                            }
                        }
                    }

                    State = new AniDBStateUpdate
                    {
                        Value = false,
                        UpdateType = BanEnum,
                        UpdateTime = DateTime.Now,
                    };
                }
            }
        }

        public ConnectionHandler(IServiceProvider provider, CommandProcessorGeneral queue, AniDBRateLimiter rateLimiter)
        {
            var factory = provider.GetService<ILoggerFactory>();
            ServiceProvider = provider;
            Logger = factory.CreateLogger(GetType());
            GeneralQueue = queue;
            RateLimiter = rateLimiter;
            BanResetTimer = new Timer
            {
                AutoReset = false, Interval = TimeSpan.FromHours(BanTimerResetLength).TotalMilliseconds
            };
            BanResetTimer.Elapsed += BanResetTimerElapsed;
        }

        ~ConnectionHandler()
        {
            BanResetTimer.Elapsed -= BanResetTimerElapsed;
        }
        
        private void BanResetTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Logger.LogInformation($"AniDB {Type} ban ({BanTimerResetLength}h) is over");
            IsBanned = false;
        }

        protected void ExtendBanTimer(int secsToPause, string pauseReason)
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

        protected void ResetBanTimer()
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

        protected void UpdateState(AniDBStateUpdate args)
        {
            AniDBStateUpdate?.Invoke(this, args);
        }
    }
}
