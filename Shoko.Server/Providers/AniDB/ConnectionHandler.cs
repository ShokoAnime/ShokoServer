using System;
using System.Timers;
using Microsoft.Extensions.Logging;

#nullable enable
namespace Shoko.Server.Providers.AniDB;

public abstract class ConnectionHandler
{
    protected readonly ILoggerFactory _loggerFactory;
    protected ILogger Logger { get; set; }
    public abstract double BanTimerResetLength { get; }
    public abstract string Type { get; }
    protected abstract UpdateType BanEnum { get; }

    public event EventHandler<AniDBStateUpdate>? AniDBStateUpdate;

    private AniDBStateUpdate? _currentState;

    public AniDBStateUpdate State
    {
        get => _currentState ??= new AniDBStateUpdate { Value = false, UpdateType = BanEnum, UpdateTime = DateTime.Now };
        set
        {
            if (value is not null && value != _currentState)
            {
                _currentState = value;
                UpdateState(_currentState!);
            }
        }
    }

    protected int? BackoffSecs { get; set; }
    private readonly Timer _backoffTimer;
    private readonly Timer _banResetTimer;
    public DateTime? BanTime { get; set; }
    private bool _isBanned;

    public virtual bool IsBanned
    {
        get => _isBanned;
        set
        {
            _isBanned = value;
            if (value)
            {
                BanTime = DateTime.Now;
                Logger.LogWarning("AniDB {Type} Banned!", Type);
                if (_banResetTimer.Enabled)
                {
                    Logger.LogWarning("AniDB {Type} ban timer was already running, ban time extending", Type);
                    _banResetTimer.Stop(); //re-start implies stop
                }

                _banResetTimer.Start();
                State = new AniDBStateUpdate
                {
                    Value = true,
                    UpdateType = BanEnum,
                    UpdateTime = DateTime.Now,
                    PauseTimeSecs = (int)TimeSpan.FromHours(BanTimerResetLength).TotalSeconds
                };
            }
            else
            {
                if (_banResetTimer.Enabled)
                {
                    _banResetTimer.Stop();
                    Logger.LogInformation("AniDB {Type} ban timer stopped. Resuming queue if not paused", Type);
                }

                State = new AniDBStateUpdate { Value = false, UpdateType = BanEnum, UpdateTime = DateTime.Now };
            }
        }
    }

    protected ConnectionHandler(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        Logger = loggerFactory.CreateLogger(GetType());
        _banResetTimer = new Timer
        {
            AutoReset = false,
            Interval = TimeSpan.FromHours(BanTimerResetLength).TotalMilliseconds
        };
        _banResetTimer.Elapsed += BanResetTimerElapsed;
        _backoffTimer = new Timer { AutoReset = false };
        _backoffTimer.Elapsed += ResetBackoffTimer;
    }

    ~ConnectionHandler()
    {
        _banResetTimer.Elapsed -= BanResetTimerElapsed;
        _backoffTimer.Elapsed -= ResetBackoffTimer;
    }

    private void BanResetTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        Logger.LogInformation("AniDB {Type} ban ({BanTimerResetLength}h) is over", Type, BanTimerResetLength);
        IsBanned = false;
    }

    protected void StartBackoffTimer(int secsToPause, string pauseReason)
    {
        // This Handles the Waiting Period For When AniDB is under heavy load. Not likely to be used
        BackoffSecs = secsToPause;
        _backoffTimer.Interval = secsToPause * 1000;
        _backoffTimer.Start();
        AniDBStateUpdate?.Invoke(this,
            new AniDBStateUpdate
            {
                UpdateType = UpdateType.OverloadBackoff,
                Value = true,
                UpdateTime = DateTime.Now,
                PauseTimeSecs = secsToPause,
                Message = pauseReason
            });
    }

    protected void ResetBackoffTimer(object? sender, ElapsedEventArgs args)
    {
        // This Handles the Waiting Period For When AniDB is under heavy load. Not likely to be used
        BackoffSecs = null;
        AniDBStateUpdate?.Invoke(this,
            new AniDBStateUpdate { UpdateType = UpdateType.OverloadBackoff, Value = false, UpdateTime = DateTime.Now });
    }

    protected void UpdateState(AniDBStateUpdate args)
    {
        AniDBStateUpdate?.Invoke(this, args);
    }
}
