using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the networks of orderings: a global ordering's, stored through the
/// studio store and removed with it, a user's ordering's, of any source with
/// stubs for the unknown ones, the default ordering's, which are the series'
/// own, and a TMDB episode group's. A stored network reads itself through
/// <c>RepoFactory</c>, so these tests share its collection.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class MetadataOrderingNetworkTests
{
    #region Helpers

    private static readonly MetadataGuid _tokyoMX = new(TestSources.Plugin, MetadataEntityType.Network, "n1");

    private static readonly MetadataGuid _bs11 = new(TestSources.Plugin, MetadataEntityType.Network, "n2");

    private static readonly MetadataGuid _atx = new(TestSources.Plugin, MetadataEntityType.Network, "n3");

    private static readonly MetadataGuid _fujiTV = new(MetadataSource.TMDB, MetadataEntityType.Network, "82");

    /// <summary>
    /// A series the service can find, and the service over in-memory tables
    /// with two stored networks.
    /// </summary>
    private sealed class World
    {
        private readonly Dictionary<MetadataGuid, ISeries> _series = [];

        public OrderingTables Tables { get; } = new();

        public MetadataOrderingService Service { get; }

        public Mock<IMetadataService> Metadata { get; } = new();

        public World()
        {
            Metadata.Setup(service => service.GetSeries(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => _series.GetValueOrDefault(id));
            Service = Tables.Build(() => Metadata.Object);
            Tables.StudioStore.SaveNetworks(
            [
                new() { ID = _tokyoMX, Name = "Tokyo MX", CountryOfOrigin = "JP" },
                new() { ID = _bs11, Name = "BS11" },
            ]);
        }

        public ISeries AddSeries(string id, params INetwork[] networks)
        {
            var series = new Mock<ISeries>();
            series.SetupGet(value => value.ID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, id));
            series.SetupGet(value => value.Seasons).Returns([]);
            series.SetupGet(value => value.Episodes).Returns([]);
            series.SetupGet(value => value.Networks).Returns(networks);
            _series[series.Object.ID] = series.Object;
            return series.Object;
        }

        public IReadOnlyList<MetadataGuid> StoredNetworks(MetadataGuid orderingID)
            => [.. Tables.NetworkEntries.GetByEntry(orderingID).OrderBy(row => row.Ordering).Select(row => Tables.Networks.GetByID(row.NetworkID)!).Select(row => ((IMetadata)row).ID)];

        public Metadata_Network? Stored(MetadataGuid network)
            => Tables.Networks.GetByProviderID(network.Source, network.ID);
    }

    private static MetadataLocalOrderingData Local(ISeries series, IReadOnlyList<MetadataGuid>? networks)
        => new() { SeriesID = series.ID, Networks = networks };

    private static MetadataOrderingData Global(ISeries series, params MetadataGuid[] networks)
        => new()
        {
            ID = new(TestSources.Plugin, MetadataEntityType.Ordering, "dvd"),
            SeriesID = series.ID,
            Type = OrderingType.DVD,
            Networks = networks,
        };

    #endregion

    #region Global Orderings

    [Fact]
    public void AGlobalOrderingsNetworksAreStoredInOrderAndGoWithIt()
    {
        var world = new World();
        var series = world.AddSeries("a");
        using var scope = new RepoFactoryScope().Set(world.Tables.Networks);

        var saved = world.Service.SaveOrdering(Global(series, _bs11, _tokyoMX));

        Assert.Equal([_bs11, _tokyoMX], saved.Networks.Select(network => network.ID));
        var read = world.Service.GetOrdering(saved.ID)!;
        Assert.Equal(["BS11", "Tokyo MX"], read.Networks.Select(network => network.Name));
        Assert.Equal([null, "JP"], read.Networks.Select(network => network.CountryOfOrigin));

        world.Service.SaveOrdering(Global(series, _tokyoMX));
        Assert.Equal([_tokyoMX], world.StoredNetworks(saved.ID));

        Assert.True(world.Service.RemoveOrdering(saved.ID));
        Assert.Empty(world.StoredNetworks(saved.ID));
        Assert.NotNull(world.Tables.Networks.GetByProviderID(TestSources.Plugin, "n1")!.LastOrphanedAt);
    }

    [Fact]
    public void AGlobalOrderingNamingAnUnstoredOrForeignNetworkIsRefusedWhole()
    {
        var world = new World();
        var series = world.AddSeries("a");
        var ordering = Global(series);

        Assert.Throws<ArgumentException>(() => world.Service.SaveOrdering(Global(series, new MetadataGuid(TestSources.Plugin, MetadataEntityType.Network, "missing"))));
        Assert.Throws<ArgumentException>(() => world.Service.SaveOrdering(Global(series, new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Network, "1"))));
        Assert.Null(world.Service.GetOrdering(ordering.ID));
    }

    [Fact]
    public void RemovingASeriesTakesItsOrderingsNetworks()
    {
        var world = new World();
        var series = world.AddSeries("a");
        var saved = world.Service.SaveOrdering(Global(series, _tokyoMX));

        Assert.Equal(1, world.Service.RemoveForSeries(series.ID));

        Assert.Empty(world.StoredNetworks(saved.ID));
    }

    #endregion

    #region Users' Orderings

    [Fact]
    public void AUsersOrderingLinksNetworksOfAnySourceAndStubsTheOnesNotStored()
    {
        var world = new World();
        var series = world.AddSeries("a");
        using var scope = new RepoFactoryScope().Set(world.Tables.Networks);

        var created = world.Service.CreateLocalOrdering(Local(series, [_atx, _tokyoMX, _fujiTV]));

        Assert.Equal([_atx, _tokyoMX, _fujiTV], world.StoredNetworks(created.ID));
        Assert.Equal(["", "Tokyo MX", ""], created.Networks.Select(network => network.Name));
        Assert.True(world.Stored(_atx)!.IsStub);
        Assert.True(world.Stored(_fujiTV)!.IsStub);
        Assert.False(world.Stored(_tokyoMX)!.IsStub);
        Assert.Null(world.Stored(_atx)!.LastOrphanedAt);
    }

    [Fact]
    public void AUsersOrderingReadsANetworkAsItsSourceServesIt()
    {
        var world = new World();
        var series = world.AddSeries("a");
        var fujiTV = new Mock<INetwork>();
        fujiTV.SetupGet(network => network.ID).Returns(_fujiTV);
        fujiTV.SetupGet(network => network.Name).Returns("Fuji TV");
        world.Metadata.Setup(service => service.GetEntry(_fujiTV)).Returns(fujiTV.Object);
        using var scope = new RepoFactoryScope().Set(world.Tables.Networks);

        var created = world.Service.CreateLocalOrdering(Local(series, [_fujiTV]));

        Assert.Equal("Fuji TV", Assert.Single(world.Service.GetOrdering(created.ID)!.Networks).Name);
    }

    [Fact]
    public void AnUpdateKeepsTheNetworksWhenLeftOutAndReplacesThemWhenGiven()
    {
        var world = new World();
        var series = world.AddSeries("a");
        using var scope = new RepoFactoryScope().Set(world.Tables.Networks);
        var created = world.Service.CreateLocalOrdering(Local(series, [_atx, _fujiTV]));

        world.Service.UpdateLocalOrdering(created.ID, Local(series, null));
        Assert.Equal([_atx, _fujiTV], world.StoredNetworks(created.ID));

        world.Service.UpdateLocalOrdering(created.ID, Local(series, [_bs11]));
        Assert.Equal([_bs11], world.StoredNetworks(created.ID));

        // The stubs wait for the orphan purge, TMDB's as a plugin's.
        Assert.NotNull(world.Stored(_atx)!.LastOrphanedAt);
        Assert.NotNull(world.Stored(_fujiTV)!.LastOrphanedAt);
    }

    [Fact]
    public void AProvidersSaveFillsInAStubThatStaysLinked()
    {
        var world = new World();
        var series = world.AddSeries("a");
        using var scope = new RepoFactoryScope().Set(world.Tables.Networks);
        var created = world.Service.CreateLocalOrdering(Local(series, [_atx]));
        var stubID = world.Stored(_atx)!.Metadata_NetworkID;

        world.Tables.StudioStore.SaveNetworks([new() { ID = _atx, Name = "AT-X", CountryOfOrigin = "JP" }]);

        var saved = world.Stored(_atx)!;
        Assert.Equal((stubID, false, "AT-X", null), (saved.Metadata_NetworkID, saved.IsStub, saved.Name, saved.LastOrphanedAt));
        Assert.Equal("AT-X", Assert.Single(world.Service.GetOrdering(created.ID)!.Networks).Name);
    }

    [Fact]
    public void ThePurgeKeepsALinkedStubAndDropsAnUnlinkedOne()
    {
        var world = new World();
        var series = world.AddSeries("a");
        var gone = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Network, "n4");
        using var scope = new RepoFactoryScope().Set(world.Tables.Networks);
        var created = world.Service.CreateLocalOrdering(Local(series, [_atx, gone]));
        world.Service.UpdateLocalOrdering(created.ID, Local(series, [_atx]));

        var removed = world.Tables.StudioStore.RemoveOrphaned(TestSources.Plugin, DateTime.MaxValue);

        Assert.Contains(gone, removed);
        Assert.DoesNotContain(_atx, removed);
        Assert.True(world.Stored(_atx)!.IsStub);
        Assert.Null(world.Stored(gone));
    }

    [Fact]
    public void DeletingAUsersOrderingTakesItsNetworks()
    {
        var world = new World();
        var series = world.AddSeries("a");
        using var scope = new RepoFactoryScope().Set(world.Tables.Networks);
        var created = world.Service.CreateLocalOrdering(Local(series, [_atx, _fujiTV]));

        Assert.True(world.Service.DeleteLocalOrdering(created.ID));

        Assert.Empty(world.StoredNetworks(created.ID));
        Assert.NotNull(world.Stored(_atx)!.LastOrphanedAt);
        Assert.NotNull(world.Stored(_fujiTV)!.LastOrphanedAt);
    }

    [Fact]
    public void AUsersOrderingRefusesANetworkOfASourceThisServerDoesNotKnow()
    {
        var world = new World();
        var series = world.AddSeries("a");
        Assert.True(MetadataSource.TryParse("not-installed", out var unknown));

        Assert.Throws<ArgumentException>(() => world.Service.CreateLocalOrdering(Local(series, [new(unknown, MetadataEntityType.Network, "1")])));
        Assert.Throws<ArgumentException>(() => world.Service.CreateLocalOrdering(Local(series, [new(TestSources.Plugin, MetadataEntityType.Studio, "1")])));
        Assert.Empty(world.Service.GetStoredOrderings(MetadataSource.User));
    }

    [Fact]
    public void APluginCannotWriteNetworksUnderTheUserSource()
    {
        var world = new World();
        var series = world.AddSeries("a");
        var created = world.Service.CreateLocalOrdering(Local(series, []));

        Assert.Throws<ArgumentException>(() => world.Tables.StudioStore.SetNetworks(created.ID, [_tokyoMX]));
        Assert.Throws<ArgumentException>(() => world.Tables.StudioStore.RemoveNetworks(created.ID));
        Assert.Throws<ArgumentException>(() => world.Service.SaveOrdering(Global(series, _tokyoMX) with { ID = new(MetadataSource.User, MetadataEntityType.Ordering, "x") }));
        Assert.Empty(world.StoredNetworks(created.ID));
    }

    #endregion

    #region Default Orderings

    [Fact]
    public void TheDefaultOrderingHasTheSeriesNetworks()
    {
        var world = new World();
        var network = Mock.Of<INetwork>();
        var series = world.AddSeries("a", network);

        Assert.Same(network, Assert.Single(world.Service.GetDefaultOrdering(series).Networks));
    }

    #endregion
}
