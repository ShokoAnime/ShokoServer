using System;
using System.Threading.Tasks;
using Moq;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.Plugin.Tmdb;
using Shoko.Plugin.Tmdb.Api;
using Xunit;

namespace Shoko.Tests.Plugin.Tmdb;

/// <summary>
///   The plugin's rate limiter: the waits it holds for a 429 and for server
///   errors, kept apart, and the suspensions it reports for them.
/// </summary>
public sealed class TmdbRateLimiterTests
{
    private readonly ManualTimeProvider _clock = new();

    private readonly Mock<ISuspensionReporter<TmdbSuspensionProvider>> _reporter = new();

    private TmdbRateLimiter Create(TimeSpan? errorWindow = null)
        => new(new TmdbRateLimitConfiguration { MaxRequestsPerWindow = 40, WindowDurationMs = 1000 }, _clock, errorWindow: errorWindow, reporter: _reporter.Object);

    [Fact]
    public void ARateLimitHoldsForAsLongAsTmdbAsksAndIsReportedWithItsEnd()
    {
        using var limiter = Create();
        var until = _clock.GetUtcNow() + TimeSpan.FromSeconds(30);

        limiter.NotifyRateLimitExceeded(TimeSpan.FromSeconds(30));

        Assert.Equal(until, limiter.RateLimitedUntil);
        Assert.Null(limiter.ServerErrorsUntil);
        _reporter.Verify(r => r.Suspend(SuspensionKind.RateLimited, null, until.UtcDateTime, false), Times.Once);

        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Null(limiter.ResumesAt);
    }

    [Fact]
    public void TheTwoWaitsAreKeptApartAndARequestWaitsForTheLater()
    {
        using var limiter = Create(TimeSpan.FromSeconds(10));
        limiter.NotifyRateLimitExceeded(TimeSpan.FromSeconds(30));
        limiter.NotifyServerError();
        limiter.NotifyServerError();
        limiter.NotifyServerError();

        Assert.Equal(_clock.GetUtcNow() + TimeSpan.FromSeconds(30), limiter.RateLimitedUntil);
        Assert.Equal(_clock.GetUtcNow() + TmdbRateLimiter.GetServerErrorPause(1), limiter.ServerErrorsUntil);
        Assert.Equal(limiter.ServerErrorsUntil, limiter.ResumesAt);
        _reporter.Verify(r => r.Suspend(SuspensionKind.ServerErrors, null, It.IsAny<DateTime?>(), false), Times.Once);

        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Null(limiter.RateLimitedUntil);
        Assert.NotNull(limiter.ServerErrorsUntil);
    }

    [Fact]
    public void AShorterPauseDoesNotCutALongerOneShort()
    {
        using var limiter = Create();
        limiter.NotifyRateLimitExceeded(TimeSpan.FromMinutes(5));
        var resumesAt = limiter.ResumesAt;

        limiter.NotifyRateLimitExceeded(TimeSpan.FromSeconds(1));

        Assert.Equal(resumesAt, limiter.ResumesAt);
        _reporter.Verify(r => r.Suspend(SuspensionKind.RateLimited, null, It.IsAny<DateTime?>(), false), Times.Once);
    }

    [Fact]
    public void ThreeServerErrorsInTheWindowPauseAndEscalate()
    {
        using var limiter = Create(TimeSpan.FromSeconds(10));

        limiter.NotifyServerError();
        limiter.NotifyServerError();
        Assert.Null(limiter.ServerErrorsUntil);

        limiter.NotifyServerError();
        Assert.Equal(_clock.GetUtcNow() + TmdbRateLimiter.GetServerErrorPause(1), limiter.ServerErrorsUntil);

        _clock.Advance(TmdbRateLimiter.GetServerErrorPause(1));
        Assert.Null(limiter.ServerErrorsUntil);

        // A second trip before any request succeeded pauses for longer.
        limiter.NotifyServerError();
        limiter.NotifyServerError();
        limiter.NotifyServerError();
        Assert.Equal(_clock.GetUtcNow() + TmdbRateLimiter.GetServerErrorPause(2), limiter.ResumesAt);
    }

    [Fact]
    public void ServerErrorsSpreadOutDoNotPause()
    {
        using var limiter = Create(TimeSpan.FromSeconds(10));

        limiter.NotifyServerError();
        _clock.Advance(TimeSpan.FromSeconds(6));
        limiter.NotifyServerError();
        _clock.Advance(TimeSpan.FromSeconds(6));
        limiter.NotifyServerError();

        Assert.Null(limiter.ServerErrorsUntil);
    }

    [Fact]
    public void ASuccessAfterThePauseResetsTheEscalation()
    {
        using var limiter = Create(TimeSpan.FromSeconds(10));
        limiter.NotifyServerError();
        limiter.NotifyServerError();
        limiter.NotifyServerError();

        // Not while the pause is on.
        limiter.NotifySuccess();
        Assert.Equal(1, limiter.ServerErrorLevel);

        _clock.Advance(TmdbRateLimiter.GetServerErrorPause(1));
        limiter.NotifySuccess();
        Assert.Equal(0, limiter.ServerErrorLevel);
    }

    [Fact]
    public async Task ARequestWaitsOutThePause()
    {
        using var limiter = Create();
        limiter.NotifyRateLimitExceeded(TimeSpan.FromSeconds(30));

        var request = limiter.EnsureRateAsync(() => Task.FromResult(1), TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(request.IsCompleted);

        _clock.Advance(TimeSpan.FromSeconds(31));

        Assert.Equal(1, await request.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }
}
