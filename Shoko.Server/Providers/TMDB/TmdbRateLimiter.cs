using System;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Events;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Providers.TMDB;

/// <summary>
/// Rate limiter for the TMDB API (~40 req/sec enforced by TMDB).
/// Uses a fixed window to enforce a local request cap and adapts to server-enforced
/// 429 backoff via <see cref="NotifyRateLimitExceeded"/>.
/// </summary>
public sealed class TmdbRateLimiter : IDisposable
{
    private readonly ILogger<TmdbRateLimiter> _logger;

    private readonly ConfigurationProvider<ServerSettings> _settingsProvider;

    private volatile SlidingWindowRateLimiter _limiter;

    private volatile int _maxRequestsPerWindow;

    private long _backoffUntilTicks;

    private readonly CancellationTokenSource _disposeCts = new();

    /// <summary>
    /// Number of requests recorded in the current window.
    /// </summary>
    public int CallsInWindow =>
        _maxRequestsPerWindow - (int)(_limiter.GetStatistics()?.CurrentAvailablePermits ?? _maxRequestsPerWindow);

    /// <summary>
    /// Remaining request capacity in the current window.
    /// </summary>
    public int RemainingInWindow =>
        (int)(_limiter.GetStatistics()?.CurrentAvailablePermits ?? _maxRequestsPerWindow);

    public TmdbRateLimiter(ILogger<TmdbRateLimiter> logger, ConfigurationProvider<ServerSettings> settingsProvider)
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        var settings = settingsProvider.Load().TMDB.RateLimit;
        _maxRequestsPerWindow = settings.MaxRequestsPerWindow;
        _limiter = CreateLimiter(settings.MaxRequestsPerWindow, settings.WindowDurationMs);
        _settingsProvider.Saved += OnSettingsSaved;
    }

    public void Dispose()
    {
        _settingsProvider.Saved -= OnSettingsSaved;
        _disposeCts.Cancel();
        _disposeCts.Dispose();
        _limiter.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnSettingsSaved(object? sender, ConfigurationSavedEventArgs<ServerSettings> eventArgs)
    {
        var settings = _settingsProvider.Load().TMDB.RateLimit;
        _maxRequestsPerWindow = settings.MaxRequestsPerWindow;
        var oldLimiter = _limiter;
        _limiter = CreateLimiter(settings.MaxRequestsPerWindow, settings.WindowDurationMs);
        // Dispose the old limiter after a grace period to let any in-flight AcquireAsync calls complete.
        _ = Task.Delay(TimeSpan.FromSeconds(15))
            .ContinueWith(_ => oldLimiter.Dispose(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    private static SlidingWindowRateLimiter CreateLimiter(int maxRequests, int windowMs)
        => new(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = maxRequests,
            Window = TimeSpan.FromMilliseconds(windowMs),
            SegmentsPerWindow = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = int.MaxValue,
            AutoReplenishment = true,
        });

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
    /// Blocks if the current window is full or a server 429 backoff is active.
    /// </summary>
    public async Task<T> EnsureRateAsync<T>(Func<Task<T>> action)
    {
        await WaitForBackoffAsync(_disposeCts.Token);
        using var lease = await _limiter.AcquireAsync(1, _disposeCts.Token);
        // Re-check backoff: a 429 may have arrived from a concurrent caller after
        // WaitForBackoffAsync returned but before we acquired the slot.
        await WaitForBackoffAsync(_disposeCts.Token);
        return await action();
    }

    private async Task WaitForBackoffAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var backoffTicks = Interlocked.Read(ref _backoffUntilTicks);
            if (backoffTicks == 0 || DateTimeOffset.UtcNow.UtcTicks >= backoffTicks)
                return;

            var wait = new DateTimeOffset(backoffTicks, TimeSpan.Zero) - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                var jitter = Jitter();
                _logger.LogTrace("TMDB server backoff active. Waiting {Wait}ms", (wait + jitter).TotalMilliseconds);
                await Task.Delay(wait + jitter, cancellationToken);
            }
        }
    }

    internal static TimeSpan Jitter() => TimeSpan.FromMilliseconds(Random.Shared.Next(0, 50));
}
