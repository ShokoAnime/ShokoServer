using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Plugin.Tmdb;
using Shoko.Plugin.Tmdb.Api;
using TMDbLib.Objects.Exceptions;
using TMDbLib.Objects.TvShows;
using Xunit;

namespace Shoko.Tests.Plugin.Tmdb;

/// <summary>
///   The plugin's TMDB client over the fixtures: what it answers for an entry
///   TMDB lacks, how it retries, and how it feeds the rate limiter.
/// </summary>
public sealed class TmdbApiClientTests
{
    private readonly TmdbRoutes _routes = new();

    [Fact]
    public async Task AnEntryTmdbLacksIsNull()
    {
        using var client = TmdbTestClient.Create(_routes);

        Assert.Null(await client.GetShow(404, TvShowMethods.Undefined, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WithoutAnApiKeyNothingIsAsked()
    {
        using var client = TmdbTestClient.Create(_routes, new TmdbConfiguration());

        Assert.False(client.HasApiKey);
        await Assert.ThrowsAsync<MetadataProviderNotConfiguredException>(() => client.GetShow(1001, TvShowMethods.Undefined, TestContext.Current.CancellationToken));
        Assert.Empty(_routes.Paths);
    }

    [Fact]
    public async Task ARateLimitIsWaitedOutAndRetried()
    {
        _routes.Status("tv/1001", (HttpStatusCode)429, TimeSpan.FromMilliseconds(50)).Fixture("tv/1001", "show-1001.json");
        using var client = TmdbTestClient.Create(_routes);

        var show = await client.GetShow(1001, TvShowMethods.Undefined, TestContext.Current.CancellationToken);

        Assert.Equal(1001, show?.Id);
        Assert.Equal(2, _routes.Count("tv/1001"));
        Assert.Null(client.RateLimiter.ResumesAt);
    }

    [Fact]
    public async Task AServerErrorIsThrownAndCountsTowardsThePause()
    {
        _routes.Status("tv/1001", HttpStatusCode.ServiceUnavailable);
        using var client = TmdbTestClient.Create(_routes);

        for (var attempt = 0; attempt < 3; attempt++)
            await Assert.ThrowsAsync<GeneralHttpException>(() => client.GetShow(1001, TvShowMethods.Undefined, TestContext.Current.CancellationToken));

        Assert.NotNull(client.RateLimiter.ServerErrorsUntil);
    }

    [Fact]
    public async Task TheShowsChangesNameTheSeasonsAndEpisodes()
    {
        _routes.Fixture("tv/1001/changes", "changes-show-1001.json");
        using var client = TmdbTestClient.Create(_routes);

        var changes = await client.GetShowChanges(1001, DateTime.UtcNow.AddHours(-1), TestContext.Current.CancellationToken);

        Assert.NotNull(changes);
        Assert.Equal([1], changes.SeasonNumbers.Order());
        Assert.Equal([(1, 2)], changes.Episodes);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(1, -2)]
    public async Task TheChangesAreNotAskedOutsideTheWindow(int windowDays, int lastRefreshedDaysAgo)
    {
        using var client = TmdbTestClient.Create(_routes, new TmdbConfiguration { UserApiKey = "test-key", IncrementalChangesWindowDays = windowDays });

        Assert.Null(await client.GetShowChanges(1001, DateTime.UtcNow.AddDays(lastRefreshedDaysAgo), TestContext.Current.CancellationToken));
        Assert.True(await client.HasMovieChanged(7001, DateTime.UtcNow.AddDays(lastRefreshedDaysAgo), TestContext.Current.CancellationToken));
        Assert.Empty(_routes.Paths);
    }

    [Fact]
    public async Task TheImageServerFallsBackToTmdbsOwnWithoutAKey()
    {
        using var client = TmdbTestClient.Create(_routes, new TmdbConfiguration());

        Assert.Equal(TmdbApiClient.DefaultImageServerUrl, await client.GetImageServerUrl(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    public void OnlyServerErrorsArePassing(HttpStatusCode status, bool transient)
        => Assert.Equal(transient, TmdbApiClient.IsTransient(new GeneralHttpException(status)));
}
