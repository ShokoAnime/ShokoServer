using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Events;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Providers.TMDB;

/// <summary>
/// Sliding-window rate limiter for the TMDB API (~40 req/sec enforced by TMDB).
/// Tracks real request timestamps so callers can observe <see cref="CallsInWindow"/>
/// and <see cref="RemainingInWindow"/>, and adapts to server-enforced 429 backoff
/// via <see cref="NotifyRateLimitExceeded"/>.
/// </summary>
public class TmdbRateLimiter
{
    private readonly ILogger<TmdbRateLimiter> _logger;

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly ConcurrentQueue<long> _requestTimestamps = new();

    private long _backoffUntilTicks;

    private readonly object _settingsLock = new();

    private readonly ConfigurationProvider<ServerSettings> _settingsProvider;

    private int? _maxRequestsPerWindow;

    private int? _windowDurationMs;

    private int MaxRequestsPerWindow
    {
        get
        {
            EnsureUsable();
            return _maxRequestsPerWindow!.Value;
        }
    }

    private int WindowDurationMs
    {
        get
        {
            EnsureUsable();
            return _windowDurationMs!.Value;
        }
    }

    /// <summary>
    /// Number of requests recorded in the current sliding window.
    /// </summary>
    public int CallsInWindow
    {
        get
        {
            PruneOldTimestamps();
            return _requestTimestamps.Count;
        }
    }

    /// <summary>
    /// Remaining request capacity in the current sliding window.
    /// </summary>
    public int RemainingInWindow => Math.Max(0, MaxRequestsPerWindow - CallsInWindow);

    public TmdbRateLimiter(ILogger<TmdbRateLimiter> logger, ConfigurationProvider<ServerSettings> settingsProvider)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _settingsProvider.Saved += OnSettingsSaved;
    }

    ~TmdbRateLimiter()
    {
        _settingsProvider.Saved -= OnSettingsSaved;
    }

    private void OnSettingsSaved(object? sender, ConfigurationSavedEventArgs<ServerSettings> eventArgs)
    {
        EnsureUsable(true);
    }

    private void EnsureUsable(bool force = false)
    {
        if (!force && _maxRequestsPerWindow.HasValue)
            return;

        lock (_settingsLock)
        {
            if (!force && _maxRequestsPerWindow.HasValue)
                return;

            var settings = _settingsProvider.Load().TMDB.RateLimit;
            _maxRequestsPerWindow = settings.MaxRequestsPerWindow;
            _windowDurationMs = settings.WindowDurationMs;
        }
    }

    /// <summary>
    /// Signal that TMDB returned a 429. All pending <see cref="EnsureRateAsync{T}"/> calls
    /// will pause until the backoff window elapses.
    /// </summary>
    /// <param name="retryAfter">Duration to back off; defaults to 1 second if null.</param>
    public void NotifyRateLimitExceeded(TimeSpan? retryAfter)
    {
        var delay = retryAfter ?? TimeSpan.FromSeconds(1);
        var until = DateTimeOffset.UtcNow + delay;
        Interlocked.Exchange(ref _backoffUntilTicks, until.UtcTicks);
        _logger.LogTrace("TMDB rate limit exceeded. Backing off until {Until}", until);
    }

    /// <summary>
    /// Acquire a rate-limit slot, then execute <paramref name="action"/>.
    /// The slot is recorded before the action runs; the action itself executes
    /// outside the internal lock so concurrent calls are allowed.
    /// </summary>
    public async Task<T> EnsureRateAsync<T>(Func<Task<T>> action)
    {
        await AcquireSlotAsync();
        return await action();
    }

    private void PruneOldTimestamps()
    {
        var cutoff = DateTimeOffset.UtcNow.AddMilliseconds(-WindowDurationMs).UtcTicks;
        while (_requestTimestamps.TryPeek(out var oldest) && oldest < cutoff)
            _requestTimestamps.TryDequeue(out _);
    }

    private async Task AcquireSlotAsync()
    {
        while (true)
        {
            await _semaphore.WaitAsync();

            PruneOldTimestamps();

            // Honour server-enforced backoff (set by NotifyRateLimitExceeded).
            // Do NOT clear the field here — every concurrent caller must see the backoff
            // until the timestamp naturally expires (UtcNow >= backoffTicks).
            var backoffTicks = Interlocked.Read(ref _backoffUntilTicks);
            if (backoffTicks > 0 && DateTimeOffset.UtcNow.UtcTicks < backoffTicks)
            {
                var backoffWait = new DateTimeOffset(backoffTicks, TimeSpan.Zero) - DateTimeOffset.UtcNow;
                _semaphore.Release();
                if (backoffWait > TimeSpan.Zero)
                {
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 50));
                    _logger.LogTrace("TMDB server backoff active. Waiting {Wait}ms", (backoffWait + jitter).TotalMilliseconds);
                    await Task.Delay(backoffWait + jitter);
                }
                continue;
            }

            // Slot available — record the timestamp and return.
            if (_requestTimestamps.Count < MaxRequestsPerWindow)
            {
                _requestTimestamps.Enqueue(DateTimeOffset.UtcNow.UtcTicks);
                _semaphore.Release();
                _logger.LogTrace("TMDB slot acquired. Calls in window: {Calls}/{Max}", _requestTimestamps.Count, MaxRequestsPerWindow);
                return;
            }

            // Window full — wait until the oldest recorded request expires.
            _requestTimestamps.TryPeek(out var oldestTick);
            var expiry = new DateTimeOffset(oldestTick, TimeSpan.Zero).AddMilliseconds(WindowDurationMs);
            var waitTime = expiry - DateTimeOffset.UtcNow;
            _semaphore.Release();

            if (waitTime > TimeSpan.Zero)
            {
                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 50));
                _logger.LogTrace("TMDB window full ({Calls}/{Max}). Waiting {Wait}ms", _requestTimestamps.Count, MaxRequestsPerWindow, (waitTime + jitter).TotalMilliseconds);
                await Task.Delay(waitTime + jitter);
            }
        }
    }
}
