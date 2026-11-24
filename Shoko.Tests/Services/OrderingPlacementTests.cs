using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Services.Ordering;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="OrderingPlacement"/>: the home and number of every
/// episode, where placed specials air and the viewing order.
/// </summary>
public class OrderingPlacementTests
{
    #region Helpers

    private static MetadataGuid E(string id)
        => new(MetadataSource.TMDB, MetadataEntityType.Episode, id);

    private static OrderingPlacementGroup Regular(params string[] ids)
        => new(false, [.. ids.Select(E)]);

    private static OrderingPlacementGroup Specials(params string[] ids)
        => new(true, [.. ids.Select(E)]);

    /// <summary>
    /// Each group as <c>season: id=number ...</c>.
    /// </summary>
    private static string[] Homes(OrderingPlacementResult result)
        => [.. result.Groups.Select(group => $"{group.SeasonNumber}:{string.Concat(group.Episodes.Select(episode => $" {episode.EpisodeID.ID}={episode.EpisodeNumber}"))}")];

    private static string[] Viewing(OrderingPlacementResult result)
        => [.. result.ViewingOrder.Select(id => id.ID)];

    private static PlacedSpecial Special(OrderingPlacementResult result, string id)
        => Assert.Single(result.Specials, special => special.EpisodeID == E(id));

    private static MetadataGuid G(string id)
        => new(MetadataSource.TMDB, MetadataEntityType.Season, id);

    private static int?[] Absolutes(OrderingPlaces places, string id)
        => [.. places.PlacesOf(E(id)).Select(place => place.AbsoluteEpisodeNumber)];

    private static (int?, int?, int?, string?, string?) Airs(PlacedSpecial special)
        => (special.AirsBeforeSeasonNumber, special.AirsBeforeEpisodeNumber, special.AirsAfterSeasonNumber, special.PreviousEpisodeID?.ID, special.NextEpisodeID?.ID);

    #endregion

    #region Numbering

    [Fact]
    public void WithoutASpecialGroupEveryGroupIsNumberedInOrder()
    {
        var result = OrderingPlacement.Place([Regular("a", "b"), Regular("c")]);

        Assert.Equal(["1: a=1 b=2", "2: c=1"], Homes(result));
        Assert.Equal(["a", "b", "c"], Viewing(result));
        Assert.Empty(result.Specials);
        Assert.Equal(3, result.EpisodeCount);
    }

    [Fact]
    public void NoGroupsPlaceNothing()
    {
        var result = OrderingPlacement.Place([]);

        Assert.Empty(result.Groups);
        Assert.Empty(result.ViewingOrder);
        Assert.Equal(0, result.EpisodeCount);
    }

    [Fact]
    public void EpisodesSharingAGivenNumberAreOneCombinedEntry()
    {
        var numbers = new Dictionary<MetadataGuid, int> { [E("a")] = 1, [E("b")] = 1, [E("c")] = 2 };
        var result = OrderingPlacement.Place([new(false, [E("a"), E("b"), E("c"), E("d")], numbers)]);

        Assert.Equal(["1: a=1 b=1 c=2 d=4"], Homes(result));
        Assert.Equal(["a", "b", "c", "d"], Viewing(result));
    }

    [Fact]
    public void AGivenNumberIsNotUsedForAPlacedSpecial()
    {
        var numbers = new Dictionary<MetadataGuid, int> { [E("s")] = 7, [E("b")] = 3 };
        var result = OrderingPlacement.Place([new(false, [E("a"), E("s"), E("b")], numbers), Specials("s")]);

        Assert.Equal(["1: a=1 b=3", "0: s=1"], Homes(result));
        Assert.Equal((1, 3, null, "a", "b"), Airs(Special(result, "s")));
    }

    [Fact]
    public void ContinuousNumbersGoOnAcrossTheRegularGroups()
    {
        var numbers = new Dictionary<MetadataGuid, int> { [E("c")] = 1, [E("d")] = 1 };
        var places = OrderingPlaces.ByPlace(
            [
                new(G("one"), 1, false, [E("a"), E("s"), E("b")]),
                new(G("two"), 2, false, [E("c"), E("d"), E("e")], numbers),
                new(G("specials"), 0, true, [E("s")]),
            ],
            continuous: true
        );

        Assert.Equal([1], Absolutes(places, "a"));
        Assert.Equal([2], Absolutes(places, "b"));
        Assert.Equal([3], Absolutes(places, "c"));
        Assert.Equal([3], Absolutes(places, "d"));
        Assert.Equal([5], Absolutes(places, "e"));
        Assert.Equal([null], Absolutes(places, "s"));
    }

    [Fact]
    public void PlacesHaveNoAbsoluteNumbersUnlessContinuous()
    {
        var places = OrderingPlaces.ByPlace([new(G("one"), 1, false, [E("a"), E("b")])]);

        Assert.Equal([null], Absolutes(places, "b"));
    }

    [Fact]
    public void TheSpecialGroupIsSeasonZeroWhereverItSits()
    {
        var result = OrderingPlacement.Place([Regular("a"), Specials("s"), Regular("b")]);

        Assert.Equal(["1: a=1", "0: s=1", "2: b=1"], Homes(result));
        Assert.Equal([0, 1, 2], result.Groups.Select(group => group.GroupIndex));
        Assert.True(result.Groups[1].IsSpecial);
    }

    [Fact]
    public void AnEpisodeInTwoRegularGroupsKeepsBothPlacesButIsWatchedOnce()
    {
        var result = OrderingPlacement.Place([Regular("a", "b"), Regular("b", "c"), Specials("s")]);

        Assert.Equal(["1: a=1 b=2", "2: b=1 c=2", "0: s=1"], Homes(result));
        Assert.Equal(["a", "b", "c", "s"], Viewing(result));
        Assert.Empty(result.Specials);
        Assert.Equal(4, result.EpisodeCount);
    }

    [Fact]
    public void AnEpisodeListedTwiceInTheSpecialGroupIsNumberedOnce()
    {
        var result = OrderingPlacement.Place([Specials("s", "t", "s"), Regular("a")]);

        Assert.Equal(["0: s=1 t=2", "1: a=1"], Homes(result));
        Assert.Equal(["s", "t", "a"], Viewing(result));
        Assert.Equal(3, result.EpisodeCount);
    }

    [Fact]
    public void TwoSpecialGroupsAreRefused()
        => Assert.Throws<ArgumentException>(() => OrderingPlacement.Place([Specials("s"), Regular("a"), Specials("t")]));

    #endregion

    #region Placed Specials

    [Fact]
    public void ASpecialInARegularGroupStaysHomeInSeasonZeroAndAirsBeforeTheNextEpisode()
    {
        var result = OrderingPlacement.Place([Regular("a", "s", "b"), Specials("s")]);

        Assert.Equal(["1: a=1 b=2", "0: s=1"], Homes(result));
        var special = Special(result, "s");
        Assert.Equal(1, special.SpecialNumber);
        Assert.Equal((1, 2, null, "a", "b"), Airs(special));
        Assert.Equal(["a", "s", "b"], Viewing(result));
        Assert.Equal(3, result.EpisodeCount);
    }

    [Fact]
    public void WithTheSpecialGroupFirstOnlyTheUnplacedSpecialsAreWatchedFirst()
    {
        var result = OrderingPlacement.Place([Specials("p", "u"), Regular("a", "p", "b")]);

        Assert.Equal(["0: p=1 u=2", "1: a=1 b=2"], Homes(result));
        Assert.Equal((1, 2, null, "a", "b"), Airs(Special(result, "p")));
        Assert.Equal(["u", "a", "p", "b"], Viewing(result));
    }

    [Fact]
    public void WithTheSpecialGroupLastOnlyTheUnplacedSpecialsAreWatchedLast()
    {
        var result = OrderingPlacement.Place([Regular("a", "p", "b"), Specials("u", "p")]);

        Assert.Equal(2, Special(result, "p").SpecialNumber);
        Assert.Equal(["a", "p", "b", "u"], Viewing(result));
    }

    [Fact]
    public void ASpecialAfterTheLastEpisodeOfAGroupAirsAfterTheSeason()
    {
        var result = OrderingPlacement.Place([Specials("s"), Regular("a", "b", "s"), Regular("c")]);

        Assert.Equal((null, null, 1, "b", "c"), Airs(Special(result, "s")));
        Assert.Equal(["a", "b", "s", "c"], Viewing(result));
    }

    [Fact]
    public void ASpecialAfterTheLastEpisodeOfTheLastGroupHasNothingAfterIt()
        => Assert.Equal((null, null, 2, "b", null), Airs(Special(OrderingPlacement.Place([Regular("a"), Regular("b", "s"), Specials("s")]), "s")));

    [Fact]
    public void ASpecialBeforeTheFirstEpisodeAirsBeforeEpisodeOne()
    {
        var result = OrderingPlacement.Place([Specials("s", "t"), Regular("s", "a"), Regular("t", "b")]);

        Assert.Equal((1, 1, null, null, "a"), Airs(Special(result, "s")));
        Assert.Equal((2, 1, null, "a", "b"), Airs(Special(result, "t")));
        Assert.Equal(["s", "a", "t", "b"], Viewing(result));
    }

    [Fact]
    public void ASpecialPlacedTwiceAirsAtItsFirstPlace()
    {
        var result = OrderingPlacement.Place([Regular("a", "s", "b"), Regular("c", "s", "d"), Specials("s")]);

        Assert.Equal(["1: a=1 b=2", "2: c=1 d=2", "0: s=1"], Homes(result));
        Assert.Equal((1, 2, null, "a", "b"), Airs(Special(result, "s")));
        Assert.Equal(["a", "s", "b", "c", "d"], Viewing(result));
        Assert.Equal(5, result.EpisodeCount);
    }

    [Fact]
    public void SpecialsBetweenTheSameEpisodesKeepTheirRegularGroupOrder()
    {
        var result = OrderingPlacement.Place([Specials("x", "y", "z"), Regular("a", "z", "x", "y", "b")]);

        Assert.All(result.Specials, special => Assert.Equal((1, 2, null, "a", "b"), Airs(special)));
        Assert.Equal(["z", "x", "y"], result.Specials.Select(special => special.EpisodeID.ID));
        Assert.Equal([3, 1, 2], result.Specials.Select(special => special.SpecialNumber));
        Assert.Equal(["a", "z", "x", "y", "b"], Viewing(result));
    }

    [Fact]
    public void AGroupOfOnlyPlacedSpecialsKeepsItsNumberAndHomesNothing()
    {
        var result = OrderingPlacement.Place([Regular("a"), Regular("s"), Regular("b"), Specials("s")]);

        Assert.Equal(["1: a=1", "2:", "3: b=1", "0: s=1"], Homes(result));
        Assert.Equal((null, null, 2, "a", "b"), Airs(Special(result, "s")));
        Assert.Equal(["a", "s", "b"], Viewing(result));
    }

    #endregion
}
