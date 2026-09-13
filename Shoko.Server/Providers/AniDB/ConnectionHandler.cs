using System;
using System.Diagnostics.CodeAnalysis;
using System.Timers;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Events;

namespace Shoko.Server.Providers.AniDB;

public abstract class ConnectionHandler
{
    protected readonly ILoggerFactory _loggerFactory;

    protected ILogger Logger { get; set; }

    public double BanTimerResetLength => BanState.ResetLengthHours;

    public abstract string Type { get; }

    protected abstract UpdateType BanEnum { get; }

    public event EventHandler<AniDBStateUpdate>? AniDBStateUpdate;

    public event EventHandler<AnidbBanOccurredEventArgs>? BanOccurred;

    public event EventHandler<AnidbBanOccurredEventArgs>? BanExpired;

    /// <summary>
    /// The ban state for this protocol. The single source of truth for whether
    /// we are banned, when the ban started and when it expires; all times are
    /// in UTC.
    /// </summary>
    public AniDbBanState BanState { get; }

    private AniDBStateUpdate? _currentState;

    public AniDBStateUpdate State
    {
        get => _currentState ??= new() { Value = false, UpdateTime = DateTime.Now };
        set
        {
            if (value is null) return;
            _currentState = value;
            UpdateState(_currentState);
        }
    }

    protected int? BackoffSecs { get; set; }

    private readonly Timer _backoffTimer;

    /// <summary>
    /// When the current ban was registered, in UTC.
    /// </summary>
    public DateTime? BanTime => BanState.BanTime;

    [MemberNotNullWhen(true, nameof(BanTime))]
    public virtual bool IsBanned
    {
        get => BanState.IsBanned;
        set
        {
            if (value)
                BanState.Ban();
            else
                BanState.Unban();
        }
    }

    protected ConnectionHandler(ILoggerFactory loggerFactory, AniDbBanState banState)
    {
        _loggerFactory = loggerFactory;
        Logger = loggerFactory.CreateLogger(GetType());
        BanState = banState;
        BanState.BanOccurred += OnBanStateOccurred;
        BanState.BanExpired += OnBanStateExpired;
        _backoffTimer = new Timer { AutoReset = false };
        _backoffTimer.Elapsed += ResetBackoffTimer;
    }

    ~ConnectionHandler()
    {
        BanState.BanOccurred -= OnBanStateOccurred;
        BanState.BanExpired -= OnBanStateExpired;
        _backoffTimer.Elapsed -= ResetBackoffTimer;
    }

    private void OnBanStateOccurred(object? sender, AnidbBanOccurredEventArgs e)
    {
        State = new()
        {
            Value = true,
            UpdateType = BanEnum,
            UpdateTime = DateTime.Now,
            PauseTimeSecs = (int)TimeSpan.FromHours(BanTimerResetLength).TotalSeconds,
        };
        BanOccurred?.Invoke(this, e);
    }

    private void OnBanStateExpired(object? sender, AnidbBanOccurredEventArgs e)
    {
        State = new()
        {
            Value = false,
            UpdateType = BanEnum,
            UpdateTime = DateTime.Now,
        };
        BanExpired?.Invoke(this, e);
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
