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

public class TmdbMetadataServiceTests
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
        var (seasons, episodes) = TmdbMetadataService.ParseShowChanges([]);

        Assert.Empty(seasons);
        Assert.Empty(episodes);
    }

    [Fact]
    public void EpisodeChange_ExtractsSeasonAndEpisodePair()
    {
        var changes = new List<Change> { EpisodeChange(new EpisodePair(2, 5)) };

        var (seasons, episodes) = TmdbMetadataService.ParseShowChanges(changes);

        Assert.Contains(2, seasons);
        Assert.Contains((2, 5), episodes);
    }

    [Fact]
    public void EpisodeChange_AddsSeasonNumberToSeasonSet()
    {
        var changes = new List<Change> { EpisodeChange(new EpisodePair(3, 1)) };

        var (seasons, _) = TmdbMetadataService.ParseShowChanges(changes);

        Assert.Contains(3, seasons);
    }

    [Fact]
    public void SeasonChange_AddsToSeasonSetOnly()
    {
        var changes = new List<Change> { SeasonChange(1) };

        var (seasons, episodes) = TmdbMetadataService.ParseShowChanges(changes);

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

        var (seasons, episodes) = TmdbMetadataService.ParseShowChanges(changes);

        Assert.Empty(seasons);
        Assert.Empty(episodes);
    }

    [Fact]
    public void MultipleEpisodesAcrossSeasons_AllCaptured()
    {
        var changes = new List<Change> { EpisodeChange(new EpisodePair(1, 3), new EpisodePair(2, 7), new EpisodePair(2, 8)) };

        var (seasons, episodes) = TmdbMetadataService.ParseShowChanges(changes);

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

        var (seasons, episodes) = TmdbMetadataService.ParseShowChanges(changes);

        Assert.Equal(new HashSet<int> { 1, 2 }, seasons);
        Assert.Contains((1, 5), episodes);
        Assert.DoesNotContain((2, 0), episodes);
    }

    [Fact]
    public void DuplicateEpisode_DeduplicatedInOutput()
    {
        var changes = new List<Change>
        {
            EpisodeChange(new EpisodePair(1, 4)),
            EpisodeChange(new EpisodePair(1, 4)),
        };

        var (_, episodes) = TmdbMetadataService.ParseShowChanges(changes);

        Assert.Single(episodes);
        Assert.Contains((1, 4), episodes);
    }

    [Fact]
    public void AddedItem_ValueExtracted()
    {
        var changes = new List<Change>
        {
            new()
            {
                Key = "episode",
                Items = [new ChangeItemAdded { Value = EpisodeValue(1, 2) }],
            },
        };

        var (_, episodes) = TmdbMetadataService.ParseShowChanges(changes);

        Assert.Contains((1, 2), episodes);
    }

    [Fact]
    public void DestroyedItem_ValueExtracted()
    {
        var changes = new List<Change>
        {
            new()
            {
                Key = "episode",
                Items = [new ChangeItemDestroyed { Value = EpisodeValue(2, 3) }],
            },
        };

        var (_, episodes) = TmdbMetadataService.ParseShowChanges(changes);

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

        var (_, episodes) = TmdbMetadataService.ParseShowChanges(changes);

        Assert.Contains((3, 9), episodes);
    }

    [Fact]
    public void ItemWithNoValue_Skipped()
    {
        var changes = new List<Change>
        {
            new()
            {
                Key = "episode",
                Items = [new ChangeItemUpdated { Value = null }],
            },
        };

        var (seasons, episodes) = TmdbMetadataService.ParseShowChanges(changes);

        Assert.Empty(seasons);
        Assert.Empty(episodes);
    }

    [Fact]
    public void ItemWithNonJObjectValue_Skipped()
    {
        var changes = new List<Change>
        {
            new()
            {
                Key = "episode",
                Items = [new ChangeItemUpdated { Value = "unexpected string" }],
            },
        };

        var (seasons, episodes) = TmdbMetadataService.ParseShowChanges(changes);

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

        var (seasons, episodes) = TmdbMetadataService.ParseShowChanges(changes);

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

        var (seasons, episodes) = TmdbMetadataService.ParseShowChanges(changes);

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

    [Fact]
    public void HttpRequestException_IsTransient()
        => Assert.True(TmdbMetadataService.IsTmdbTransient(new HttpRequestException()));

    [Fact]
    public void RequestLimitExceededException_IsTransient()
        => Assert.True(TmdbMetadataService.IsTmdbTransient(MakeRequestLimitExceeded()));

    [Fact]
    public void NotFoundException_IsNotTransient()
        => Assert.False(TmdbMetadataService.IsTmdbTransient(new NotFoundException(null!)));

    [Fact]
    public void GeneralHttpException_IsNotTransient()
        => Assert.False(TmdbMetadataService.IsTmdbTransient(new GeneralHttpException(System.Net.HttpStatusCode.InternalServerError)));

    [Fact]
    public void TmdbApiKeyUnavailableException_IsNotTransient()
        => Assert.False(TmdbMetadataService.IsTmdbTransient(new TmdbApiKeyUnavailableException()));

    [Fact]
    public void ArbitraryException_IsNotTransient()
        => Assert.False(TmdbMetadataService.IsTmdbTransient(new InvalidOperationException("unexpected")));
}
