using System;
using Moq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.TMDB;
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
    public void AnOrderingOfAnotherSourceIsNoEpisodeGroup()
    {
        var ordering = new Mock<IOrdering>();
        ordering.SetupGet(o => o.ID).Returns(new MetadataGuid(MetadataSource.User, MetadataEntityType.Ordering, "local"));
        ordering.SetupGet(o => o.SeriesID).Returns(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "5"));

        Assert.Null(TmdbCompatibility.AlternateOrdering.From(ordering.Object));
    }
}
