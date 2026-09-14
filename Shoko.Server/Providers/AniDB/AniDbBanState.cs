using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Events;

namespace Shoko.Server.Providers.AniDB;

/// <summary>
/// The ban state of one AniDB protocol (UDP or HTTP). All members are
/// lock-protected and thread-safe.
/// </summary>
/// <remarks>
/// A ban is idempotent: issuing a ban while one is still active neither
/// re-stamps the start time nor extends the expiry (re-stamping used to
/// silently extend an active ban). A ban that has already elapsed is a new
/// cycle and is re-stamped. <see cref="Unban"/> fires <see cref="BanExpired"/>
/// at most once per ban. The ban auto-expires: a <see cref="PeriodicTimer"/>
/// watchdog lifts it once <see cref="BanExpiresUtc"/> has passed, instead of
/// the previous one-shot <see cref="System.Timers.Timer"/>.
/// </remarks>
public sealed class AniDbBanState
{
    /// <summary>
    /// Hours an AniDB UDP ban stays in effect before it is lifted automatically.
    /// </summary>
    public const double UdpResetLengthHours = 1.5;

    /// <summary>
    /// Hours an AniDB HTTP ban stays in effect before it is lifted automatically.
    /// </summary>
    public const double HttpResetLengthHours = 12;

    // Bans last at least 1.5 hours, so a minute of expiry resolution is ample.
    private static readonly TimeSpan ExpiryCheckInterval = TimeSpan.FromMinutes(1);

    private readonly object _lock = new();
    private readonly AnidbBanType _type;
    private readonly double _resetLengthHours;
    private readonly ILogger _logger;
    private readonly PeriodicTimer _expiryTimer = new(ExpiryCheckInterval);

    private bool _isBanned;
    private DateTime? _banTimeUtc;
    private DateTime? _banExpiresUtc;

    public AniDbBanState(AnidbBanType type, double resetLengthHours, ILogger logger)
    {
        _type = type;
        _resetLengthHours = resetLengthHours;
        _logger = logger;
        _ = WatchForExpiryAsync();
    }

    /// <summary>
    /// The protocol this ban state belongs to.
    /// </summary>
    public AnidbBanType BanType => _type;

    /// <summary>
    /// How long, in hours, a registered ban stays in effect before it is lifted.
    /// </summary>
    public double ResetLengthHours => _resetLengthHours;

    /// <summary>
    /// Whether a ban is currently active.
    /// </summary>
    [MemberNotNullWhen(true, nameof(BanTime))]
    public bool IsBanned
    {
        get
        {
            lock (_lock) return _isBanned;
        }
    }

    /// <summary>
    /// When the active ban was registered, in UTC. Null when not banned.
    /// </summary>
    public DateTime? BanTime
    {
        get
        {
            lock (_lock) return _banTimeUtc;
        }
    }

    /// <summary>
    /// When the active ban is expected to expire, in UTC. Null when not banned.
    /// </summary>
    public DateTime? BanExpiresUtc
    {
        get
        {
            lock (_lock) return _banExpiresUtc;
        }
    }

    /// <summary>
    /// Dispatched when a ban is registered.
    /// </summary>
    public event EventHandler<AnidbBanOccurredEventArgs>? BanOccurred;

    /// <summary>
    /// Dispatched when a ban is lifted, either manually or on expiry.
    /// </summary>
    public event EventHandler<AnidbBanOccurredEventArgs>? BanExpired;

    /// <summary>
    /// Registers a ban. Idempotent for a ban that is still active; a ban that
    /// has already elapsed starts a fresh cycle.
    /// </summary>
    public void Ban()
    {
        AnidbBanOccurredEventArgs? args = null;
        lock (_lock)
        {
            var nowUtc = DateTime.UtcNow;
            if (_isBanned && _banExpiresUtc is { } expiresUtc && expiresUtc > nowUtc)
                return;

            _isBanned = true;
            _banTimeUtc = nowUtc;
            _banExpiresUtc = nowUtc.AddHours(_resetLengthHours);

            args = new AnidbBanOccurredEventArgs
            {
                Type = _type,
                OccurredAt = nowUtc,
                ExpiresAt = _banExpiresUtc.Value,
            };
            _logger.LogWarning("AniDB {Type} Banned! (expires {Expires:u})", _type, _banExpiresUtc);
        }
        if (args is not null)
            BanOccurred?.Invoke(this, args);
    }

    /// <summary>
    /// Lifts the ban. Fires <see cref="BanExpired"/> at most once per ban; a
    /// call while not banned is a no-op.
    /// </summary>
    public void Unban()
    {
        AnidbBanOccurredEventArgs? args = null;
        lock (_lock)
        {
            if (!_isBanned)
                return;

            var nowUtc = DateTime.UtcNow;
            var occurredAt = _banTimeUtc ?? nowUtc;
            _isBanned = false;
            _banTimeUtc = null;
            _banExpiresUtc = null;

            args = new AnidbBanOccurredEventArgs
            {
                Type = _type,
                OccurredAt = occurredAt,
                ExpiresAt = nowUtc,
            };
            _logger.LogInformation("AniDB {Type} ban ({ResetLength}h) is over", _type, _resetLengthHours);
        }
        if (args is not null)
            BanExpired?.Invoke(this, args);
    }

    private async Task WatchForExpiryAsync()
    {
        while (await _expiryTimer.WaitForNextTickAsync())
        {
            var expired = false;
            lock (_lock)
            {
                expired = _isBanned && _banExpiresUtc is { } expiresUtc && expiresUtc <= DateTime.UtcNow;
            }
            if (expired)
                Unban();
        }
    }
}
