using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Server.Providers.Anilist;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Providers.Anilist;

public class AnilistRateLimiterTests
{
    [Fact]
    public async Task CallsWithinWindow_ProceedImmediately()
    {
        using var limiter = CreateRateLimiter(maxRequests: 3, windowMs: 1000);
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < 3; i++)
            await limiter.EnsureRateAsync(() => Task.FromResult(0));

        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(200), $"Expected < 200ms for 3 calls within a 3-request window, got {sw.Elapsed.TotalMilliseconds:F0}ms");
    }

    [Fact]
    public async Task ExtraCall_WaitsForWindowSlot()
    {
        using var limiter = CreateRateLimiter(maxRequests: 2, windowMs: 1000);

        await limiter.EnsureRateAsync(() => Task.FromResult(0));
        await limiter.EnsureRateAsync(() => Task.FromResult(0));

        var sw = Stopwatch.StartNew();
        await limiter.EnsureRateAsync(() => Task.FromResult(0));

        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(300), $"Expected a wait for the 3rd call with a 2-request window, got {sw.Elapsed.TotalMilliseconds:F0}ms");
    }

    [Fact]
    public async Task RateLimitExceeded_PausesUntilRetryAfter()
    {
        using var limiter = CreateRateLimiter(maxRequests: 10, windowMs: 1000);
        limiter.NotifyRateLimitExceeded(TimeSpan.FromMilliseconds(300));

        var sw = Stopwatch.StartNew();
        await limiter.EnsureRateAsync(() => Task.FromResult(0));

        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(250), $"Expected the call to wait for the 429 backoff, got {sw.Elapsed.TotalMilliseconds:F0}ms");
    }

    [Fact]
    public async Task ExhaustedQuota_WithReset_PausesUntilReset()
    {
        using var limiter = CreateRateLimiter(maxRequests: 10, windowMs: 1000);
        limiter.NotifyQuota(null, 0, DateTimeOffset.UtcNow.AddMilliseconds(300));

        Assert.Equal(0, limiter.RemainingRequests);
        var sw = Stopwatch.StartNew();
        await limiter.EnsureRateAsync(() => Task.FromResult(0));

        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(250), $"Expected the call to wait for the quota reset, got {sw.Elapsed.TotalMilliseconds:F0}ms");
    }

    [Fact]
    public async Task RemainingQuota_DoesNotPause()
    {
        using var limiter = CreateRateLimiter(maxRequests: 10, windowMs: 1000);
        limiter.NotifyQuota(null, 5, DateTimeOffset.UtcNow.AddMinutes(1));

        var sw = Stopwatch.StartNew();
        await limiter.EnsureRateAsync(() => Task.FromResult(0));

        Assert.Equal(5, limiter.RemainingRequests);
        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(200), $"Expected no wait while quota remains, got {sw.Elapsed.TotalMilliseconds:F0}ms");
    }

    [Fact]
    public void FirstServerError_TripsBreaker_AndPauseStateChangedFires()
    {
        using var limiter = CreateRateLimiter();
        var fired = 0;
        limiter.PauseStateChanged += (_, _) => fired++;

        limiter.Notify5xxError();

        Assert.True(limiter.Is5xxPaused);
        Assert.Equal(1, fired);
        Assert.True(limiter.GetPauseSnapshot().IsPaused);
        Assert.True(limiter.BackoffUntilTicks > DateTimeOffset.UtcNow.UtcTicks);
    }

    [Fact]
    public void ServerErrorsWhilePaused_DoNotEscalate()
    {
        using var limiter = CreateRateLimiter();
        var fired = 0;
        limiter.PauseStateChanged += (_, _) => fired++;

        limiter.Notify5xxError();
        var firstDeadline = limiter.BackoffUntilTicks;
        limiter.Notify5xxError();
        limiter.Notify5xxError();

        Assert.Equal(firstDeadline, limiter.BackoffUntilTicks);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Defaults_AreOneRequestPerFourSeconds()
    {
        var settings = new ServerSettings().Anilist.RateLimit;

        Assert.Equal(1, settings.MaxRequestsPerWindow);
        Assert.Equal(4000, settings.WindowDurationMs);
    }

    [Fact]
    public void AdvertisedLimit_IsScaledToTheLocalWindow_AndOnlyLowers()
    {
        // 10 per 4s locally is 150 per minute; AniList advertising 30 per minute allows 2 per 4s.
        using var limiter = CreateRateLimiter(maxRequests: 10, windowMs: 4000);
        limiter.NotifyQuota(30, 29, null);
        Assert.Equal(2, limiter.MaxRequestsPerWindow);

        // Advertising more than we're configured for changes nothing.
        limiter.NotifyQuota(900, 899, null);
        Assert.Equal(10, limiter.MaxRequestsPerWindow);

        // A tiny allowance never drops below one request per window.
        limiter.NotifyQuota(1, 0, null);
        Assert.Equal(1, limiter.MaxRequestsPerWindow);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 5)]
    [InlineData(5, 60)]
    [InlineData(9, 60)]
    public void Get5xxPauseDuration_Escalates_AndCaps(int level, int expectedMinutes)
        => Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), AnilistRateLimiter.Get5xxPauseDuration(level));

    private static AnilistRateLimiter CreateRateLimiter(int maxRequests = 3, int windowMs = 1000, int errorWindowMs = 10_000)
    {
        var settings = new ServerSettings();
        settings.Anilist.RateLimit.MaxRequestsPerWindow = maxRequests;
        settings.Anilist.RateLimit.WindowDurationMs = windowMs;

        var mockService = new Mock<IConfigurationService>();
        mockService
            .Setup(s => s.GetConfigurationInfo<ServerSettings>())
            .Returns((ConfigurationInfo)null!);
        mockService
            .Setup(s => s.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>()))
            .Returns(settings);

        var provider = new ConfigurationProvider<ServerSettings>(mockService.Object);
        return new AnilistRateLimiter(NullLogger<AnilistRateLimiter>.Instance, provider, TimeSpan.FromMilliseconds(errorWindowMs));
    }
}
