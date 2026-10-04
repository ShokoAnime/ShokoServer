using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.TMDB;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers the APIv3 shape of a TMDB show's orderings now that the episode
/// groups are global orderings kept by the core: the IDs, the type names and
/// the flags come out as before.
/// </summary>
public class TmdbOrderingOutputTests
{
    private static TmdbCompatibility.AlternateOrdering Group()
    {
        var ordering = new Mock<IOrdering>();
        ordering.SetupGet(o => o.ID).Returns(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Ordering, "5f0c1a2b3c4d5e6f7a8b9c0d"));
        ordering.SetupGet(o => o.SeriesID).Returns(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "5"));
        ordering.SetupGet(o => o.Name).Returns("DVD Order");
        ordering.SetupGet(o => o.Type).Returns(OrderingType.DVD);
        ordering.SetupGet(o => o.Seasons).Returns(Array.Empty<ISeason>());
        return TmdbCompatibility.AlternateOrdering.From(ordering.Object)!;
    }

    [Fact]
    public void TheDefaultOrderingIsNamedByTheShow()
    {
        var json = JObject.FromObject(new TmdbShow.OrderingInformation(5, 24, 2, 2, isPreferred: true, inUse: true));

        Assert.Equal("5", json[nameof(TmdbShow.OrderingInformation.OrderingID)]?.Value<string>());
        Assert.Equal("Seasons", json[nameof(TmdbShow.OrderingInformation.OrderingName)]?.Value<string>());
        Assert.False(json.ContainsKey(nameof(TmdbShow.OrderingInformation.OrderingType)));
        Assert.True(json[nameof(TmdbShow.OrderingInformation.IsDefault)]?.Value<bool>());
    }

    [Fact]
    public void AnEpisodeGroupKeepsItsCollectionIDAndItsTypeName()
    {
        var group = Group();

        var json = JObject.FromObject(new TmdbShow.OrderingInformation(group, null, group));

        Assert.Equal("5f0c1a2b3c4d5e6f7a8b9c0d", json[nameof(TmdbShow.OrderingInformation.OrderingID)]?.Value<string>());
        Assert.Equal("DVD", json[nameof(TmdbShow.OrderingInformation.OrderingType)]?.Value<string>());
        Assert.False(json[nameof(TmdbShow.OrderingInformation.IsDefault)]?.Value<bool>());
        Assert.False(json[nameof(TmdbShow.OrderingInformation.IsPreferred)]?.Value<bool>());
        Assert.True(json[nameof(TmdbShow.OrderingInformation.InUse)]?.Value<bool>());
    }

    [Fact]
    public void AnOrderingListsItsSpecialsGroupLast()
    {
        var groups = new[] { 0, 2, 1 }.Select(number =>
        {
            var season = new Mock<ISeason>();
            season.SetupGet(s => s.ID).Returns(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Season, $"group{number}"));
            season.SetupGet(s => s.SeasonNumber).Returns(number);
            return season.Object;
        }).ToArray();
        var ordering = new Mock<IOrdering>();
        ordering.SetupGet(o => o.ID).Returns(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Ordering, "5f0c1a2b3c4d5e6f7a8b9c0d"));
        ordering.SetupGet(o => o.SeriesID).Returns(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "5"));
        ordering.SetupGet(o => o.Seasons).Returns(groups);

        var seasons = TmdbCompatibility.AlternateOrdering.From(ordering.Object)!.Seasons;

        Assert.Equal([1, 2, 0], seasons.Select(season => season.SeasonNumber));
    }

    [Theory]
    [InlineData(true, new[] { 101, 102, 103, 104 }, new[] { 1, 2, 3, 4 }, 4)]
    [InlineData(false, new[] { 101, 102, 104 }, new[] { 1, 2, 3 }, 3)]
    public void ASpecialAlsoInARegularGroupIsListedThereOnlyWhenAskedFor(bool includeSpecialsInSeasons, int[] listed, int[] numbers, int airsBefore)
    {
        var episodes = new[] { 101, 102, 103, 104 }.ToDictionary(
            id => new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Episode, id.ToString()),
            id => Mock.Of<IEpisode>(episode => episode.ID == new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Episode, id.ToString()))
        );
        var seriesID = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "5");
        var series = Mock.Of<ISeries>(value => value.ID == seriesID && value.Episodes == episodes.Values.ToList());
        var metadata = new Mock<IMetadataService>();
        metadata.Setup(service => service.GetSeries(seriesID)).Returns(series);
        metadata.Setup(service => service.GetEpisode(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => episodes.GetValueOrDefault(id));
        var service = new OrderingTables().Build(() => metadata.Object);
        MetadataGuid Episode(int id) => new(MetadataSource.TMDB, MetadataEntityType.Episode, id.ToString());
        var stored = service.SaveOrdering(new()
        {
            ID = new(MetadataSource.TMDB, MetadataEntityType.Ordering, "5f0c1a2b3c4d5e6f7a8b9c0d"),
            SeriesID = seriesID,
            Name = "Air Date",
            Type = OrderingType.OriginalAirDate,
            Groups =
            [
                new() { ID = new(MetadataSource.TMDB, MetadataEntityType.Season, "g0"), Name = "Specials", IsSpecial = true, Episodes = [Episode(103)] },
                new() { ID = new(MetadataSource.TMDB, MetadataEntityType.Season, "g3"), Name = "Season 3", SeasonNumber = 3, Episodes = [.. episodes.Keys] },
            ],
        });

        var ordering = TmdbCompatibility.AlternateOrdering.From(stored)!;
        var season = ordering.Seasons.Single(group => group.SeasonNumber is 3).GetEpisodes(includeSpecialsInSeasons);
        var special = Assert.Single(ordering.Seasons.Single(group => group.SeasonNumber is 0).GetEpisodes(includeSpecialsInSeasons));

        Assert.Equal(listed, season.Select(place => place.TmdbEpisodeID));
        Assert.Equal(numbers, season.Select(place => place.PlacedEpisodeNumber));
        Assert.Equal((0, 1, 3, airsBefore), (special.SeasonNumber, special.PlacedEpisodeNumber, special.AirsBeforeSeasonNumber, special.AirsBeforeEpisodeNumber));
        Assert.Equal(includeSpecialsInSeasons ? 5 : 4, ordering.GetEpisodes(includeSpecialsInSeasons).Count);
    }

    [Fact]
    public void AnOrderingOfAnotherSourceIsNoEpisodeGroup()
    {
        var ordering = new Mock<IOrdering>();
        ordering.SetupGet(o => o.ID).Returns(new MetadataGuid(MetadataSource.User, MetadataEntityType.Ordering, "local"));
        ordering.SetupGet(o => o.SeriesID).Returns(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "5"));

        Assert.Null(TmdbCompatibility.AlternateOrdering.From(ordering.Object));
    }
}
