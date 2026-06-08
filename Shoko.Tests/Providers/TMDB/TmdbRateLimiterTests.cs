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
        var limiter = CreateRateLimiter(maxRequests: 3, windowMs: 500);
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < 3; i++)
            await limiter.EnsureRateAsync(() => Task.FromResult(0));

        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(200),
            $"Expected < 200ms for 3 calls within a 3-request window, got {sw.Elapsed.TotalMilliseconds:F0}ms");
    }

    [Fact]
    public async Task ExtraCall_WaitsForWindowSlot()
    {
        var limiter = CreateRateLimiter(maxRequests: 2, windowMs: 300);

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
        var limiter = CreateRateLimiter(maxRequests: 2, windowMs: 200);

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
        var limiter = CreateRateLimiter(maxRequests: 10, windowMs: 1000);

        limiter.NotifyRateLimitExceeded(TimeSpan.FromMilliseconds(300));

        var sw = Stopwatch.StartNew();
        await limiter.EnsureRateAsync(() => Task.FromResult(0));

        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(250),
            $"Expected >= 250ms backoff delay, got {sw.Elapsed.TotalMilliseconds:F0}ms");
    }

    [Fact]
    public async Task BackoffAppliesToAllConcurrentCallers_NotSequentially()
    {
        var limiter = CreateRateLimiter(maxRequests: 10, windowMs: 1000);
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
        var limiter = CreateRateLimiter(maxRequests: 5, windowMs: 2000);

        Assert.Equal(0, limiter.CallsInWindow);

        await limiter.EnsureRateAsync(() => Task.FromResult(0));
        Assert.Equal(1, limiter.CallsInWindow);

        await limiter.EnsureRateAsync(() => Task.FromResult(0));
        Assert.Equal(2, limiter.CallsInWindow);
    }

    [Fact]
    public async Task RemainingInWindow_ReflectsRemainingCapacity()
    {
        var limiter = CreateRateLimiter(maxRequests: 3, windowMs: 2000);

        Assert.Equal(3, limiter.RemainingInWindow);

        await limiter.EnsureRateAsync(() => Task.FromResult(0));
        Assert.Equal(2, limiter.RemainingInWindow);

        await limiter.EnsureRateAsync(() => Task.FromResult(0));
        Assert.Equal(1, limiter.RemainingInWindow);
    }

    private static TmdbRateLimiter CreateRateLimiter(int maxRequests = 3, int windowMs = 200)
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
        return new TmdbRateLimiter(NullLogger<TmdbRateLimiter>.Instance, provider);
    }
}
