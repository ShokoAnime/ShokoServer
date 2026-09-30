using System;
using Moq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.Options;
using Shoko.Server.API.v3.Models.Ordering;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Pins the APIv3 shape of a series' ordering: its full and local IDs, its
/// type by name, whether a user made it, its own and its groups' images, and
/// its groups only when asked for.
/// </summary>
public class SeriesOrderingOutputTests
{
    private static IImage Image(ImageEntityType type, bool preferred)
    {
        var image = new Mock<IImage>();
        image.SetupGet(value => value.ID).Returns(Guid.NewGuid());
        image.SetupGet(value => value.Type).Returns(type);
        image.SetupGet(value => value.Source).Returns(MetadataSource.User);
        image.SetupGet(value => value.ResourceID).Returns("0123");
        image.SetupGet(value => value.IsEnabled).Returns(true);
        image.SetupGet(value => value.IsPreferred).Returns(preferred);
        return image.Object;
    }

    private static IOrdering Ordering(MetadataSource source)
    {
        var seriesID = new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Series, "3");
        var episode = new Mock<IEpisode>();
        episode.SetupGet(value => value.ID).Returns(new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Episode, "12"));
        var group = new Mock<ISeason>();
        group.SetupGet(value => value.ID).Returns(new MetadataGuid(source, MetadataEntityType.Season, "g1"));
        group.SetupGet(value => value.Title).Returns("Part 1");
        group.SetupGet(value => value.SeasonNumber).Returns(0);
        group.SetupGet(value => value.IsSpecial).Returns(true);
        group.SetupGet(value => value.Episodes).Returns([episode.Object, episode.Object]);
        group.As<IWithImages>().Setup(value => value.GetImages(It.IsAny<ImageFilteringOptions?>())).Returns([Image(ImageEntityType.Backdrop, false)]);
        var ordering = new Mock<IOrdering>();
        ordering.SetupGet(value => value.ID).Returns(new MetadataGuid(source, MetadataEntityType.Ordering, "o1"));
        ordering.SetupGet(value => value.SeriesID).Returns(seriesID);
        ordering.SetupGet(value => value.Name).Returns("Mine");
        ordering.SetupGet(value => value.Overview).Returns(string.Empty);
        ordering.SetupGet(value => value.Type).Returns(source == MetadataSource.User ? OrderingType.User : OrderingType.DVD);
        ordering.SetupGet(value => value.IsPreferred).Returns(true);
        ordering.SetupGet(value => value.EpisodeCount).Returns(1);
        ordering.SetupGet(value => value.SeasonCount).Returns(1);
        ordering.SetupGet(value => value.Seasons).Returns([group.Object]);
        ordering.SetupGet(value => value.CreatedAt).Returns(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        ordering.SetupGet(value => value.LastUpdatedAt).Returns(new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc));
        ordering.As<IWithImages>().Setup(value => value.GetImages(It.IsAny<ImageFilteringOptions?>())).Returns([Image(ImageEntityType.Primary, true)]);
        return ordering.Object;
    }

    [Fact]
    public void AUsersOrderingIsLocalAndNamedByBothIDs()
    {
        var model = new SeriesOrdering(Ordering(MetadataSource.User), includeGroups: true);

        Assert.Equal("user://ordering/o1", model.ID);
        Assert.Equal("o1", model.LocalID);
        Assert.True(model.IsLocal);
        var group = Assert.Single(model.Groups!);
        Assert.Equal("user://season/g1", group.ID);
        Assert.Equal([1, 2], [group.Episodes[0].EpisodeNumber, group.Episodes[1].EpisodeNumber]);
        Assert.Equal(12, group.Episodes[0].ShokoEpisodeID);
        Assert.Equal("shoko://episode/12", group.Episodes[0].ID);
    }

    [Fact]
    public void APluginsOrderingIsNotLocalAndItsGroupsAreLeftOutUnlessAskedFor()
    {
        var model = new SeriesOrdering(Ordering(MetadataSource.TMDB), includeGroups: false);
        var json = JObject.FromObject(model);

        Assert.False(model.IsLocal);
        Assert.Null(model.Groups);
        Assert.False(json.ContainsKey(nameof(SeriesOrdering.Groups)));
        Assert.Equal("DVD", json[nameof(SeriesOrdering.Type)]?.Value<string>());
    }
}
