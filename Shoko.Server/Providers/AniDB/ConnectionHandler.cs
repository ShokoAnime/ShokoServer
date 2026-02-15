using System;
using System.Diagnostics.CodeAnalysis;
using System.Timers;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Events;

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

    public event EventHandler<AnidbBanOccurredEventArgs>? BanOccurred;

    public event EventHandler<AnidbBanOccurredEventArgs>? BanExpired;

    private AniDBStateUpdate? _currentState;

    public AniDBStateUpdate State
    {
        get => _currentState ??= new() { Value = false, UpdateTime = DateTime.Now };
        set
        {
            if (value is not null && value != State)
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

    [MemberNotNullWhen(true, nameof(BanTime))]
    public virtual bool IsBanned
    {
        get => BanTime.HasValue;
        set
        {
            var bannedAt = BanTime;
            var now = DateTime.Now;
            if (value)
            {
                Logger.LogWarning("AniDB {Type} Banned!", Type);
                if (_banResetTimer.Enabled)
                {
                    Logger.LogWarning("AniDB {Type} ban timer was already running, ban time extending", Type);
                    _banResetTimer.Stop(); //re-start implies stop
                }

                _banResetTimer.Start();
                BanTime = now;
                State = new()
                {
                    Value = true,
                    UpdateType = BanEnum,
                    UpdateTime = now,
                    PauseTimeSecs = (int)TimeSpan.FromHours(BanTimerResetLength).TotalSeconds,
                };
                try
                {
                    BanOccurred?.Invoke(this, new()
                    {
                        Type = BanEnum is UpdateType.HTTPBan ? AnidbBanType.HTTP : AnidbBanType.UDP,
                        OccurredAt = now.ToUniversalTime(),
                        ExpiresAt = now.AddHours(BanTimerResetLength).ToUniversalTime(),
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "AniDB {Type} ban occurred event failed", Type);
                }
            }
            else
            {
                if (_banResetTimer.Enabled)
                {
                    _banResetTimer.Stop();
                    Logger.LogInformation("AniDB {Type} ban timer stopped. Resuming queue if not paused", Type);
                }

                BanTime = null;
                State = new() { Value = false, UpdateType = BanEnum, UpdateTime = now };
                if (bannedAt is not null)
                {
                    Logger.LogInformation("AniDB {Type} Unbanned!", Type);
                    try
                    {
                        BanExpired?.Invoke(this, new()
                        {
                            Type = BanEnum is UpdateType.HTTPBan ? AnidbBanType.HTTP : AnidbBanType.UDP,
                            OccurredAt = bannedAt.Value.ToUniversalTime(),
                            ExpiresAt = now.ToUniversalTime(),
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "AniDB {Type} ban expired event failed", Type);
                    }
                }
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
            Interval = TimeSpan.FromHours(BanTimerResetLength).TotalMilliseconds,
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
        UpdateState(new()
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
        UpdateState(new() { UpdateType = UpdateType.OverloadBackoff, Value = false, UpdateTime = DateTime.Now });
    }

    protected void UpdateState(AniDBStateUpdate args)
    {
        AniDBStateUpdate?.Invoke(this, args);
    }
}
