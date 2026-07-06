using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Providers.TMDB;

public class TmdbRateLimiterTests
{
    [Fact]
    public async Task CallsWithinWindow_ProceedImmediately()
    {
        using var limiter = CreateRateLimiter(maxRequests: 3, windowMs: 500);
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < 3; i++)
            await limiter.EnsureRateAsync(() => Task.FromResult(0));

        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(200),
            $"Expected < 200ms for 3 calls within a 3-request window, got {sw.Elapsed.TotalMilliseconds:F0}ms");
    }

    [Fact]
    public async Task ExtraCall_WaitsForWindowSlot()
    {
        using var limiter = CreateRateLimiter(maxRequests: 2, windowMs: 300);

        await limiter.EnsureRateAsync(() => Task.FromResult(0));
        await limiter.EnsureRateAsync(() => Task.FromResult(0));

        var sw = Stopwatch.StartNew();
        await limiter.EnsureRateAsync(() => Task.FromResult(0));

        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(200),
            $"Expected >= 200ms wait for 3rd call with 2-request window, got {sw.Elapsed.TotalMilliseconds:F0}ms");
    }

    [Fact]
    public async Task SlotsExpireAfterWindow_AllowsNewCalls()
    {
        using var limiter = CreateRateLimiter(maxRequests: 2, windowMs: 200);

        await limiter.EnsureRateAsync(() => Task.FromResult(0));
        await limiter.EnsureRateAsync(() => Task.FromResult(0));

        await Task.Delay(300);

        var sw = Stopwatch.StartNew();
        await limiter.EnsureRateAsync(() => Task.FromResult(0));
        await limiter.EnsureRateAsync(() => Task.FromResult(0));

        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(200),
            $"Expected < 200ms after window expiry, got {sw.Elapsed.TotalMilliseconds:F0}ms");
    }

    [Fact]
    public async Task NotifyRateLimitExceeded_DelaysSubsequentCalls()
    {
        using var limiter = CreateRateLimiter(maxRequests: 10, windowMs: 1000);

        limiter.NotifyRateLimitExceeded(TimeSpan.FromMilliseconds(300));

        var sw = Stopwatch.StartNew();
        await limiter.EnsureRateAsync(() => Task.FromResult(0));

        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(250),
            $"Expected >= 250ms backoff delay, got {sw.Elapsed.TotalMilliseconds:F0}ms");
    }

    [Fact]
    public async Task BackoffAppliesToAllConcurrentCallers_NotSequentially()
    {
        using var limiter = CreateRateLimiter(maxRequests: 10, windowMs: 1000);
        limiter.NotifyRateLimitExceeded(TimeSpan.FromMilliseconds(300));

        var sw = Stopwatch.StartNew();
        await Task.WhenAll(
            limiter.EnsureRateAsync(() => Task.FromResult(0)),
            limiter.EnsureRateAsync(() => Task.FromResult(0)),
            limiter.EnsureRateAsync(() => Task.FromResult(0))
        );

        // All three waited for the same backoff window (not 3 × 300ms).
        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(250),
            $"Expected >= 250ms, got {sw.Elapsed.TotalMilliseconds:F0}ms");
        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(900),
            $"Expected < 900ms (callers should share the wait, not queue it), got {sw.Elapsed.TotalMilliseconds:F0}ms");
    }

    [Fact]
    public async Task CallsInWindow_ReflectsCurrentCount()
    {
        using var limiter = CreateRateLimiter(maxRequests: 5, windowMs: 2000);

        Assert.Equal(0, limiter.CallsInWindow);

        await limiter.EnsureRateAsync(() => Task.FromResult(0));
        Assert.Equal(1, limiter.CallsInWindow);

        await limiter.EnsureRateAsync(() => Task.FromResult(0));
        Assert.Equal(2, limiter.CallsInWindow);
    }

    [Fact]
    public async Task RemainingInWindow_ReflectsRemainingCapacity()
    {
        using var limiter = CreateRateLimiter(maxRequests: 3, windowMs: 2000);

        Assert.Equal(3, limiter.RemainingInWindow);

        await limiter.EnsureRateAsync(() => Task.FromResult(0));
        Assert.Equal(2, limiter.RemainingInWindow);

        await limiter.EnsureRateAsync(() => Task.FromResult(0));
        Assert.Equal(1, limiter.RemainingInWindow);
    }

    [Fact]
    public async Task Notify5xxError_BelowThreshold_NoBackoff()
    {
        using var limiter = CreateRateLimiter(maxRequests: 10, windowMs: 1000);

        limiter.Notify5xxError();
        limiter.Notify5xxError();

        // Two errors — threshold not reached, no backoff applied.
        var sw = Stopwatch.StartNew();
        await limiter.EnsureRateAsync(() => Task.FromResult(0));
        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(200),
            $"Expected no backoff delay, got {sw.Elapsed.TotalMilliseconds:F0}ms");
    }

    [Fact]
    public void Notify5xxError_RampProgresses()
    {
        // Verify ramp schedule: levels 1–5 map to 1→3→5→15→60min.
        Assert.Equal(TimeSpan.FromMinutes(1),  TmdbRateLimiter.Get5xxPauseDuration(1));
        Assert.Equal(TimeSpan.FromMinutes(3),  TmdbRateLimiter.Get5xxPauseDuration(2));
        Assert.Equal(TimeSpan.FromMinutes(5),  TmdbRateLimiter.Get5xxPauseDuration(3));
        Assert.Equal(TimeSpan.FromMinutes(15), TmdbRateLimiter.Get5xxPauseDuration(4));
        Assert.Equal(TimeSpan.FromMinutes(60), TmdbRateLimiter.Get5xxPauseDuration(5));
    }

    [Fact]
    public void Notify5xxError_AfterFullCycle_CapsAt60min()
    {
        // Level 5 and beyond all return 60min (the _ arm of the switch).
        // The ramp is capped with Math.Min(level + 1, 5) — it does not wrap back to 0.
        Assert.Equal(TimeSpan.FromMinutes(60), TmdbRateLimiter.Get5xxPauseDuration(5));
        Assert.Equal(TimeSpan.FromMinutes(60), TmdbRateLimiter.Get5xxPauseDuration(6));
    }

    [Fact]
    public void Notify5xxError_ThreeRapidErrors_TripsBreaker()
    {
        // All 3 errors within a 500ms window — breaker must trip and set a backoff deadline.
        using var limiter = CreateRateLimiter(maxRequests: 10, windowMs: 1000, errorWindowMs: 500);

        limiter.Notify5xxError();
        limiter.Notify5xxError();
        limiter.Notify5xxError();

        Assert.True(limiter.BackoffUntilTicks > DateTimeOffset.UtcNow.UtcTicks,
            "Expected BackoffUntilTicks to be in the future after 3 rapid distress errors");
    }

    [Fact]
    public async Task Notify5xxError_SpreadAcrossWindow_DoesNotTrip()
    {
        // First error is older than the window — only 2 of the 3 slots are recent.
        using var limiter = CreateRateLimiter(maxRequests: 10, windowMs: 1000, errorWindowMs: 100);

        limiter.Notify5xxError();

        // Let the first error age out of the 100ms window before firing two more.
        await Task.Delay(150);

        limiter.Notify5xxError();
        limiter.Notify5xxError();

        Assert.True(limiter.BackoffUntilTicks <= DateTimeOffset.UtcNow.UtcTicks,
            "Expected no backoff when errors are spread across more than the error window");
    }

    [Fact]
    public async Task NotifySuccess_AfterPauseElapsed_ResetsRamp()
    {
        using var limiter = CreateRateLimiter(maxRequests: 10, windowMs: 1000);

        // Inject a short observable backoff to simulate "pause elapsed".
        limiter.NotifyRateLimitExceeded(TimeSpan.FromMilliseconds(200));

        // Wait for the backoff to elapse.
        await Task.Delay(300);

        // Success after pause — ramp should reset. We verify by confirming NotifySuccess
        // doesn't throw and that subsequent EnsureRateAsync proceeds immediately.
        limiter.NotifySuccess();

        var sw = Stopwatch.StartNew();
        await limiter.EnsureRateAsync(() => Task.FromResult(0));
        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(200),
            $"Expected immediate proceed after NotifySuccess reset, got {sw.Elapsed.TotalMilliseconds:F0}ms");
    }

    [Fact]
    public void NotifySuccess_AfterReset_ClearsRingBuffer_SingleSubsequent5xxDoesNotTrip()
    {
        // Arrange: trip the breaker, simulate pause expiration, reset via NotifySuccess.
        var limiter = new TmdbRateLimiter(NullLogger<TmdbRateLimiter>.Instance, CreateSettingsProvider(), TimeSpan.FromMilliseconds(500));
        limiter.Notify5xxError();
        limiter.Notify5xxError();
        limiter.Notify5xxError(); // trips at level 1 (60s), but we use short window so we can test

        // Manually set the backoff deadline to the past (expired) via reflection to simulate
        // the pause period having elapsed.
        var field = typeof(TmdbRateLimiter).GetField("_backoffUntilTicks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(limiter, DateTimeOffset.UtcNow.AddMilliseconds(-100).UtcTicks); // set to past

        limiter.NotifySuccess(); // should reset level AND clear ring buffer

        limiter.Notify5xxError(); // only 1 error — ring was cleared, should NOT trip

        Assert.Equal(0L, limiter.BackoffUntilTicks); // no backoff set after reset
    }

    private static ConfigurationProvider<ServerSettings> CreateSettingsProvider()
    {
        var settings = new ServerSettings();
        settings.TMDB.RateLimit.MaxRequestsPerWindow = 10;
        settings.TMDB.RateLimit.WindowDurationMs = 1000;

        var mockService = new Mock<IConfigurationService>();
        mockService
            .Setup(s => s.GetConfigurationInfo<ServerSettings>())
            .Returns((ConfigurationInfo)null!);
        mockService
            .Setup(s => s.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>()))
            .Returns(settings);

        return new ConfigurationProvider<ServerSettings>(mockService.Object);
    }

    private static TmdbRateLimiter CreateRateLimiter(int maxRequests = 3, int windowMs = 200, int errorWindowMs = 10_000)
    {
        var settings = new ServerSettings();
        settings.TMDB.RateLimit.MaxRequestsPerWindow = maxRequests;
        settings.TMDB.RateLimit.WindowDurationMs = windowMs;

        var mockService = new Mock<IConfigurationService>();
        mockService
            .Setup(s => s.GetConfigurationInfo<ServerSettings>())
            .Returns((ConfigurationInfo)null!);
        mockService
            .Setup(s => s.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>()))
            .Returns(settings);

        var provider = new ConfigurationProvider<ServerSettings>(mockService.Object);
        return new TmdbRateLimiter(NullLogger<TmdbRateLimiter>.Instance, provider, TimeSpan.FromMilliseconds(errorWindowMs));
    }
}
