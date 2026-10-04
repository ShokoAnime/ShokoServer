using System;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers series and episodes presented in an ordering: the routes from a place or an ordering to
/// them and back through <c>CurrentOrdering</c>, and the metadata service's lookups in an ordering.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public sealed class OrderingPresentationTests : IDisposable
{
    private readonly MetadataLookupTables _tables = new();

    private readonly RepoFactoryScope _scope;

    private static readonly MetadataGuid _seriesID = ID(MetadataEntityType.Series, "s1");

    private static readonly MetadataGuid _orderingID = ID(MetadataEntityType.Ordering, "reversed");

    private static readonly MetadataGuid _groupID = ID(MetadataEntityType.Season, "g1");

    /// <summary>
    /// A series of seven untitled episodes and an eighth left out of its ordering, which lists the
    /// seven in reverse in one group, and another series with an ordering of its own.
    /// </summary>
    public OrderingPresentationTests()
    {
        _scope = _tables.Scope();
        _scope.Set(TestTextManager.Build(_tables.TextStore, _tables.Service));
        _tables.SeriesStore.SaveSeries(new()
        {
            ID = _seriesID,
            Seasons = [new() { ID = ID(MetadataEntityType.Season, "s1-1"), SeasonNumber = 1 }],
            Episodes =
            [
                .. Enumerable.Range(1, 8).Select(number => new MetadataEpisodeData
                {
                    ID = EpisodeID(number),
                    SeasonID = ID(MetadataEntityType.Season, "s1-1"),
                    EpisodeNumber = number,
                }),
            ],
        });
        _tables.OrderingService.SaveOrdering(new()
        {
            ID = _orderingID,
            SeriesID = _seriesID,
            Groups = [new() { ID = _groupID, Episodes = [.. Enumerable.Range(1, 7).Reverse().Select(EpisodeID)] }],
        });
        _tables.SeriesStore.SaveSeries(new() { ID = ID(MetadataEntityType.Series, "s2"), Episodes = [new() { ID = ID(MetadataEntityType.Episode, "x1"), EpisodeNumber = 1 }] });
        _tables.OrderingService.SaveOrdering(new()
        {
            ID = ID(MetadataEntityType.Ordering, "other"),
            SeriesID = ID(MetadataEntityType.Series, "s2"),
            Groups = [new() { ID = ID(MetadataEntityType.Season, "g2"), Episodes = [ID(MetadataEntityType.Episode, "x1")] }],
        });
    }

    public void Dispose()
        => _scope.Dispose();

    private static MetadataGuid ID(MetadataEntityType entityType, string id)
        => new(TestSources.Plugin, entityType, id);

    private static MetadataGuid EpisodeID(int number)
        => ID(MetadataEntityType.Episode, $"e{number}");

    private static (int, string)[] Numbered(params int[] numbers)
        => [.. numbers.Select(number => (number, $"Episode {number}"))];

    [Fact]
    public void ASourceEpisodeComesBackFromItsPlaceInTheDefaultOrdering()
    {
        var episode = _tables.Service.GetEpisode(EpisodeID(1))!;

        Assert.True(episode.CurrentOrdering.IsDefault);
        Assert.Same(episode, episode.CurrentOrdering.Episode);
    }

    [Fact]
    public void AnEpisodeInAnotherOrderingIsNumberedThereAndComesBackFromItself()
    {
        var place = _tables.OrderingService.GetEpisodeOrderings(_tables.Service.GetEpisode(EpisodeID(1))!).Single(place => !place.IsDefault);
        var episode = place.Episode;

        Assert.Equal((7, "Episode 7"), (episode.EpisodeNumber, episode.Title));
        Assert.Same(place, episode.CurrentOrdering);
        Assert.Same(episode, episode.CurrentOrdering.Episode);
    }

    [Fact]
    public void AnOrderingsSeriesIsPresentedInIt()
    {
        var defaultOrdering = _tables.Service.GetEntry<IOrdering>(MetadataOrderingService.DefaultOrderingID(_seriesID))!;
        var ordering = _tables.Service.GetEntry<IOrdering>(_orderingID)!;

        Assert.Equal(defaultOrdering.ID, defaultOrdering.Series.CurrentOrdering.ID);
        Assert.Equal(_orderingID, ordering.Series.CurrentOrdering.ID);
        Assert.Equal([_groupID], ordering.Series.Seasons.Select(season => season.ID));
        Assert.Equal(Numbered(1, 2, 3, 4, 5, 6, 7), ordering.Series.Episodes.Select(episode => (episode.EpisodeNumber, episode.Title)));
        Assert.Equal(EpisodeID(7), ordering.Series.Episodes[0].ID);
    }

    [Fact]
    public void AGroupListsItsEpisodesInTheOrderingsNumbering()
    {
        var group = _tables.Service.GetSeason(_groupID)!;

        Assert.Equal(Numbered(1, 2, 3, 4, 5, 6, 7), group.Episodes.Select(episode => (episode.EpisodeNumber, episode.Title)));
        Assert.Equal(_orderingID, group.Series.CurrentOrdering.ID);
    }

    [Fact]
    public void TheServiceLooksUpAnEntryInAnOrdering()
    {
        var episode = _tables.Service.GetEpisode(EpisodeID(1), _orderingID)!;

        Assert.Equal((7, "Episode 7"), (episode.EpisodeNumber, episode.Title));
        Assert.Equal(_orderingID, _tables.Service.GetSeries(_seriesID, _orderingID)!.CurrentOrdering.ID);
    }

    [Fact]
    public void TheDefaultOrderingGivesThePlainEntry()
    {
        var defaultID = MetadataOrderingService.DefaultOrderingID(_seriesID);

        Assert.Same(_tables.Service.GetEpisode(EpisodeID(1)), _tables.Service.GetEpisode(EpisodeID(1), defaultID));
        Assert.Same(_tables.Service.GetSeries(_seriesID), _tables.Service.GetSeries(_seriesID, defaultID));
    }

    [Fact]
    public void AnotherSeriesOrderingOrAnUnplacedEpisodeGivesNothing()
    {
        var other = ID(MetadataEntityType.Ordering, "other");

        Assert.Null(_tables.Service.GetEpisode(EpisodeID(1), other));
        Assert.Null(_tables.Service.GetSeries(_seriesID, other));
        Assert.Null(_tables.Service.GetEpisode(EpisodeID(8), _orderingID));
    }
}
