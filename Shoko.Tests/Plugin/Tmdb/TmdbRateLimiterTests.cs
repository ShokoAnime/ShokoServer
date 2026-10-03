using System;
using System.Threading.Tasks;
using Shoko.Plugin.Tmdb;
using Shoko.Plugin.Tmdb.Api;
using Xunit;

namespace Shoko.Tests.Plugin.Tmdb;

/// <summary>
///   The plugin's rate limiter: the pauses it holds for a 429 and for server
///   errors, and when they lift.
/// </summary>
public sealed class TmdbRateLimiterTests
{
    private readonly ManualTimeProvider _clock = new();

    private TmdbRateLimiter Create(TimeSpan? errorWindow = null)
        => new(new TmdbRateLimitConfiguration { MaxRequestsPerWindow = 40, WindowDurationMs = 1000 }, _clock, errorWindow: errorWindow);

    [Fact]
    public void ARateLimitPausesForAsLongAsTmdbAsks()
    {
        using var limiter = Create();
        var changes = 0;
        limiter.PauseStateChanged += (_, _) => changes++;

        limiter.NotifyRateLimitExceeded(TimeSpan.FromSeconds(30));

        Assert.Equal(TmdbPauseReason.RateLimited, limiter.PauseReason);
        Assert.Equal(_clock.GetUtcNow() + TimeSpan.FromSeconds(30), limiter.ResumesAt);
        Assert.Equal(1, changes);

        _clock.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(TmdbPauseReason.None, limiter.PauseReason);
        Assert.Null(limiter.ResumesAt);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void AShorterPauseDoesNotCutALongerOneShort()
    {
        using var limiter = Create();
        limiter.NotifyRateLimitExceeded(TimeSpan.FromMinutes(5));
        var resumesAt = limiter.ResumesAt;

        limiter.NotifyRateLimitExceeded(TimeSpan.FromSeconds(1));

        Assert.Equal(resumesAt, limiter.ResumesAt);
    }

    [Fact]
    public void ThreeServerErrorsInTheWindowPauseAndEscalate()
    {
        using var limiter = Create(TimeSpan.FromSeconds(10));

        limiter.NotifyServerError();
        limiter.NotifyServerError();
        Assert.Equal(TmdbPauseReason.None, limiter.PauseReason);

        limiter.NotifyServerError();
        Assert.Equal(TmdbPauseReason.ServerErrors, limiter.PauseReason);
        Assert.Equal(_clock.GetUtcNow() + TmdbRateLimiter.GetServerErrorPause(1), limiter.ResumesAt);

        _clock.Advance(TmdbRateLimiter.GetServerErrorPause(1));
        Assert.Equal(TmdbPauseReason.None, limiter.PauseReason);

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

        Assert.Equal(TmdbPauseReason.None, limiter.PauseReason);
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
