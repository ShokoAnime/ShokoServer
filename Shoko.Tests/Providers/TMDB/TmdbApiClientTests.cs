using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Shoko.Server.Providers.TMDB;
using TMDbLib.Objects.Changes;
using TMDbLib.Objects.Exceptions;
using Xunit;

namespace Shoko.Tests.Providers.TMDB;

public class TmdbApiClientTests
{
    #region Helpers

    private readonly record struct EpisodePair(int Season, int Episode);

    private static Change EpisodeChange(params EpisodePair[] episodes)
    {
        var items = new List<ChangeItemBase>();
        foreach (var (season, episode) in episodes)
            items.Add(new ChangeItemUpdated { Value = EpisodeValue(season, episode) });
        return new Change { Key = "episode", Items = items };
    }

    private static Change SeasonChange(params int[] seasonNumbers)
    {
        var items = new List<ChangeItemBase>();
        foreach (var sn in seasonNumbers)
            items.Add(new ChangeItemUpdated { Value = SeasonValue(sn) });
        return new Change { Key = "season", Items = items };
    }

    private static JObject EpisodeValue(int season, int episode) =>
        new() { ["season_number"] = season, ["episode_number"] = episode, ["episode_id"] = episode * 100 };

    private static JObject SeasonValue(int season) =>
        new() { ["season_number"] = season };

    #endregion

    [Fact]
    public void EmptyChangeList_ReturnsEmptySets()
    {
        var (seasons, episodes) = TmdbApiClient.ParseShowChanges([]);

        Assert.Empty(seasons);
        Assert.Empty(episodes);
    }

    [Fact]
    public void EpisodeChange_ExtractsSeasonAndEpisodePair()
    {
        var changes = new List<Change> { EpisodeChange(new EpisodePair(2, 5)) };

        var (seasons, episodes) = TmdbApiClient.ParseShowChanges(changes);

        Assert.Contains(2, seasons);
        Assert.Contains((2, 5), episodes);
    }

    [Fact]
    public void SeasonChange_AddsToSeasonSetOnly()
    {
        var changes = new List<Change> { SeasonChange(1) };

        var (seasons, episodes) = TmdbApiClient.ParseShowChanges(changes);

        Assert.Contains(1, seasons);
        Assert.Empty(episodes);
    }

    [Fact]
    public void UnrelatedKey_IsIgnored()
    {
        var changes = new List<Change>
        {
            new() { Key = "name", Items = [new ChangeItemUpdated { Value = new JObject { ["value"] = "New Title" } }] },
            new() { Key = "overview", Items = [new ChangeItemUpdated { Value = new JObject { ["value"] = "New overview." } }] },
        };

        var (seasons, episodes) = TmdbApiClient.ParseShowChanges(changes);

        Assert.Empty(seasons);
        Assert.Empty(episodes);
    }

    [Fact]
    public void MultipleEpisodesAcrossSeasons_AllCaptured()
    {
        var changes = new List<Change> { EpisodeChange(new EpisodePair(1, 3), new EpisodePair(2, 7), new EpisodePair(2, 8)) };

        var (seasons, episodes) = TmdbApiClient.ParseShowChanges(changes);

        Assert.Equal(new HashSet<int> { 1, 2 }, seasons);
        Assert.Equal(new HashSet<(int, int)> { (1, 3), (2, 7), (2, 8) }, episodes);
    }

    [Fact]
    public void MixedEpisodeAndSeasonChanges_BothCaptured()
    {
        var changes = new List<Change>
        {
            EpisodeChange(new EpisodePair(1, 5)),
            SeasonChange(2),
        };

        var (seasons, episodes) = TmdbApiClient.ParseShowChanges(changes);

        Assert.Equal(new HashSet<int> { 1, 2 }, seasons);
        Assert.Contains((1, 5), episodes);
        Assert.DoesNotContain((2, 0), episodes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnAddedOrDestroyedItemIsReadFromItsValue(bool destroyed)
    {
        ChangeItemBase item = destroyed ? new ChangeItemDestroyed { Value = EpisodeValue(2, 3) } : new ChangeItemAdded { Value = EpisodeValue(2, 3) };

        var (_, episodes) = TmdbApiClient.ParseShowChanges([new() { Key = "episode", Items = [item] }]);

        Assert.Contains((2, 3), episodes);
    }

    [Fact]
    public void DeletedItem_OriginalValueExtracted()
    {
        var changes = new List<Change>
        {
            new()
            {
                Key = "episode",
                Items = [new ChangeItemDeleted { OriginalValue = EpisodeValue(3, 9) }],
            },
        };

        var (_, episodes) = TmdbApiClient.ParseShowChanges(changes);

        Assert.Contains((3, 9), episodes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unexpected string")]
    public void AnItemWithoutAnObjectValueIsSkipped(string? value)
    {
        var (seasons, episodes) = TmdbApiClient.ParseShowChanges([new() { Key = "episode", Items = [new ChangeItemUpdated { Value = value }] }]);

        Assert.Empty(seasons);
        Assert.Empty(episodes);
    }

    [Fact]
    public void EpisodeItemMissingSeasonNumber_Skipped()
    {
        var changes = new List<Change>
        {
            new()
            {
                Key = "episode",
                Items = [new ChangeItemUpdated { Value = new JObject { ["episode_number"] = 5 } }],
            },
        };

        var (seasons, episodes) = TmdbApiClient.ParseShowChanges(changes);

        Assert.Empty(seasons);
        Assert.Empty(episodes);
    }

    [Fact]
    public void EpisodeItemMissingEpisodeNumber_SeasonStillAdded()
    {
        var changes = new List<Change>
        {
            new()
            {
                Key = "episode",
                Items = [new ChangeItemUpdated { Value = new JObject { ["season_number"] = 2 } }],
            },
        };

        var (seasons, episodes) = TmdbApiClient.ParseShowChanges(changes);

        Assert.Contains(2, seasons);
        Assert.Empty(episodes);
    }
}

public class TmdbTransientExceptionTests
{
    private static readonly ConstructorInfo _rleCtor =
        typeof(RequestLimitExceededException).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];

    private static RequestLimitExceededException MakeRequestLimitExceeded()
    {
        var sm = Activator.CreateInstance(typeof(TMDbStatusMessage))!;
        return (RequestLimitExceededException)_rleCtor.Invoke([sm, null, null]);
    }

    public static TheoryData<Exception, bool> Failures() => new()
    {
        { new HttpRequestException(), true },
        { MakeRequestLimitExceeded(), true },
        { new NotFoundException(null!), false },
        { new GeneralHttpException(System.Net.HttpStatusCode.InternalServerError), false },
        { new TmdbApiKeyUnavailableException(), false },
        { new InvalidOperationException("unexpected"), false },
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public void OnlyANetworkFailureOrTheRateLimitIsTransient(Exception exception, bool transient)
        => Assert.Equal(transient, TmdbApiClient.IsTmdbTransient(exception));

    public static TheoryData<Exception, bool> ImageServerFailures() => new()
    {
        { new TmdbApiKeyUnavailableException(), true },
        { new AggregateException(new TmdbApiKeyUnavailableException()), true },
        { new AggregateException(new HttpRequestException(HttpRequestError.ConnectionError, "No route to host.")), true },
        { new InvalidOperationException("unexpected"), false },
        { new AggregateException(new InvalidOperationException(), new TmdbApiKeyUnavailableException()), false },
    };

    [Theory]
    [MemberData(nameof(ImageServerFailures))]
    public void AMissingApiKeyOrAnUnreachableTmdbFallsBackToTheDefaultImageServer(Exception exception, bool fallsBack)
    {
        var url = TmdbApiClient.FallbackImageServerUrl(exception);

        if (!fallsBack)
        {
            Assert.Null(url);
            return;
        }

        Assert.True(Uri.TryCreate(url, UriKind.Absolute, out var uri));
        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
    }
}
