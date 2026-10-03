using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Databases;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached.Metadata;
using Xunit;
using static Shoko.IntegrationTests.Sql;

namespace Shoko.IntegrationTests;

/// <summary>
/// Writes orderings, the chosen ordering and the hidden episodes through the
/// ordering service into the migrated database, then reads every table back
/// from it, so the mappings, the column types and the unique indexes are
/// checked on each backend. The chosen ordering and the hidden flags are read
/// back from the series' and episodes' own rows, a plugin's and TMDB's, and
/// the step moving TMDB's chosen episode group is run on a copy of its table.
/// A user's ordering's networks, stubs among them, and the step that lets a
/// network be a stub are checked the same way.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MetadataOrderingRoundTripTests(DatabaseMigrationFixture fixture)
{
    private static readonly MetadataSource _plugin = TestSources.Plugin;

    private static MetadataGuid ID(MetadataEntityType entityType, string id)
        => new(_plugin, entityType, id);

    /// <summary>
    /// Throws the caches away and reads every ordering table again from the
    /// database.
    /// </summary>
    private void Reload()
    {
        var services = fixture.Services;
        foreach (var repository in new ICachedRepository[]
        {
            services.GetRequiredService<Metadata_OrderingRepository>(),
            services.GetRequiredService<Metadata_Ordering_GroupRepository>(),
            services.GetRequiredService<Metadata_Ordering_EntryRepository>(),
            services.GetRequiredService<Metadata_SeriesRepository>(),
            services.GetRequiredService<Metadata_EpisodeRepository>(),
            services.GetRequiredService<Metadata_NetworkRepository>(),
            services.GetRequiredService<Metadata_Network_EntryRepository>(),
        })
            repository.Populate(displayName: false);
    }

    [Fact]
    public void WhatTheOrderingServiceWritesReadsBackFromTheDatabase()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var seriesStore = fixture.Services.GetRequiredService<IMetadataSeriesStore>();
        var orderings = fixture.Services.GetRequiredService<IMetadataOrderingService>();
        var longText = new string('o', 6000);
        var longID = new string('q', MetadataGuid.MaxIDLength);
        var seriesID = ID(MetadataEntityType.Series, "ordering-series-1");
        seriesStore.SaveSeries(new()
        {
            ID = seriesID,
            Seasons = [new() { ID = ID(MetadataEntityType.Season, "ordering-season-1"), SeasonNumber = 1 }],
            Episodes =
            [
                .. Enumerable.Range(1, 4).Select(number => new MetadataEpisodeData
                {
                    ID = ID(MetadataEntityType.Episode, number is 4 ? longID : $"ordering-episode-{number}"),
                    SeasonID = ID(MetadataEntityType.Season, "ordering-season-1"),
                    EpisodeNumber = number,
                }),
            ],
        });
        var episode = (int number) => ID(MetadataEntityType.Episode, number is 4 ? longID : $"ordering-episode-{number}");

        var global = orderings.SaveOrdering(new()
        {
            ID = ID(MetadataEntityType.Ordering, longID),
            SeriesID = seriesID,
            Name = "DVD Order",
            Overview = longText,
            Type = OrderingType.DVD,
            Groups =
            [
                new() { ID = ID(MetadataEntityType.Season, "ordering-group-1"), Name = "Disc 1", Overview = "The first disc.", Episodes = [episode(2), episode(1)] },
                new() { ID = ID(MetadataEntityType.Season, longID), Name = longText, Episodes = [episode(4), episode(1)] },
            ],
        });
        var local = orderings.CreateLocalOrdering(new()
        {
            SeriesID = seriesID,
            Name = "Mine",
            Groups =
            [
                new() { Name = "Extras", IsSpecial = true, Episodes = [episode(4)] },
                new() { Name = "All", Episodes = [episode(3), episode(2), episode(1)] },
            ],
        });
        // Replacing the global ordering updates its rows in place and drops the ones left out.
        global = orderings.SaveOrdering(new()
        {
            ID = global.ID,
            SeriesID = seriesID,
            Name = "DVD Order",
            Overview = longText,
            Type = OrderingType.DVD,
            Groups =
            [
                new() { ID = ID(MetadataEntityType.Season, longID), Name = longText, Episodes = [episode(1), episode(4)] },
                new() { ID = ID(MetadataEntityType.Season, "ordering-group-1"), Name = "Disc 1", Overview = "The first disc.", IsSpecial = true, Episodes = [episode(2)] },
            ],
        });
        // The longest ordering ID fits the chosen ordering's column.
        Assert.True(orderings.SetPreferredOrdering(seriesID, global.ID));
        Reload();
        Assert.Equal(global.ID, fixture.Services.GetRequiredService<Metadata_SeriesRepository>().GetByProviderID(_plugin, seriesID.ID)?.PreferredOrderingID);

        Assert.True(orderings.SetPreferredOrdering(seriesID, local.ID));
        Assert.True(orderings.SetEpisodeHidden(episode(4), true));
        Assert.True(orderings.SetEpisodeHidden(episode(3), true));
        Assert.True(orderings.SetEpisodeHidden(episode(3), false));

        Reload();

        var series = fixture.Services.GetRequiredService<IMetadataService>().GetSeries(seriesID);
        Assert.NotNull(series);
        var all = orderings.GetOrderings(series);
        Assert.Equal(3, all.Count);

        var readGlobal = orderings.GetOrdering(global.ID);
        Assert.NotNull(readGlobal);
        Assert.Equal("DVD Order", readGlobal.Name);
        Assert.Equal(longText, readGlobal.Overview);
        Assert.Equal(OrderingType.DVD, readGlobal.Type);
        Assert.Equal(seriesID, readGlobal.SeriesID);
        Assert.Equal([longID, "ordering-group-1"], readGlobal.Seasons.Select(group => group.ID.ID));
        Assert.Equal([1, 0], readGlobal.Seasons.Select(group => group.SeasonNumber));
        Assert.Equal([false, true], readGlobal.Seasons.Select(group => group.IsSpecial));
        Assert.Equal(longText, readGlobal.Seasons[0].Title);
        Assert.Equal("The first disc.", readGlobal.Seasons[1].DefaultOverview?.Value);
        Assert.Equal([episode(1), episode(4)], readGlobal.Seasons[0].Episodes.Select(item => item.ID));
        Assert.Equal(1, readGlobal.HiddenEpisodeCount);
        Assert.False(readGlobal.IsPreferred);

        var readLocal = orderings.GetOrdering(local.ID);
        Assert.NotNull(readLocal);
        Assert.Equal(MetadataSource.User, readLocal.ID.Source);
        Assert.Equal(OrderingType.User, readLocal.Type);
        Assert.True(readLocal.IsPreferred);
        Assert.Equal(local.ID, orderings.GetPreferredOrdering(series).ID);
        Assert.Equal([episode(4), episode(3), episode(2), episode(1)], readLocal.Episodes.Select(item => item.ID));
        Assert.Equal([0, 1], readLocal.Seasons.Select(group => group.SeasonNumber));
        Assert.Equal([true, false], readLocal.Seasons.Select(group => group.IsSpecial));

        Assert.True(orderings.IsEpisodeHidden(episode(4)));
        Assert.False(orderings.IsEpisodeHidden(episode(3)));
        var episodeRows = fixture.Services.GetRequiredService<Metadata_EpisodeRepository>();
        Assert.True(episodeRows.GetByProviderID(_plugin, longID)?.IsHidden);
        Assert.Equal(local.ID, fixture.Services.GetRequiredService<Metadata_SeriesRepository>().GetByProviderID(_plugin, seriesID.ID)?.PreferredOrderingID);

        Assert.True(orderings.DeleteLocalOrdering(local.ID));
        Assert.True(orderings.RemoveOrdering(global.ID));
        Assert.True(orderings.SetEpisodeHidden(episode(4), false));

        Reload();

        Assert.Single(orderings.GetOrderings(series));
        Assert.True(orderings.GetPreferredOrdering(series).IsDefault);
        Assert.Empty(fixture.Services.GetRequiredService<Metadata_Ordering_GroupRepository>().GetBySource(_plugin));
        Assert.Empty(fixture.Services.GetRequiredService<Metadata_Ordering_EntryRepository>().GetByOrderingID(_plugin, longID));
        Assert.Null(fixture.Services.GetRequiredService<Metadata_SeriesRepository>().GetByProviderID(_plugin, seriesID.ID)?.PreferredOrderingID);
        Assert.All(episodeRows.GetBySeriesID(_plugin, seriesID.ID), row => Assert.False(row.IsHidden));
        seriesStore.RemoveSeries(seriesID);
    }

    [Fact]
    public void AGlobalOrderingsNetworksReadBackFromTheDatabaseAndGoWithIt()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var seriesStore = fixture.Services.GetRequiredService<IMetadataSeriesStore>();
        var studios = fixture.Services.GetRequiredService<IMetadataStudioStore>();
        var orderings = fixture.Services.GetRequiredService<IMetadataOrderingService>();
        var seriesID = ID(MetadataEntityType.Series, "ordering-series-2");
        var tokyoMX = ID(MetadataEntityType.Network, "ordering-network-1");
        var bs11 = ID(MetadataEntityType.Network, "ordering-network-2");
        seriesStore.SaveSeries(new() { ID = seriesID });
        studios.SaveNetworks([new() { ID = tokyoMX, Name = "Tokyo MX", CountryOfOrigin = "JP" }, new() { ID = bs11, Name = "BS11" }]);
        var global = orderings.SaveOrdering(new()
        {
            ID = ID(MetadataEntityType.Ordering, "ordering-networks"),
            SeriesID = seriesID,
            Name = "Broadcast Order",
            Type = OrderingType.OriginalAirDate,
            Networks = [bs11, tokyoMX],
        });

        Reload();

        var read = orderings.GetOrdering(global.ID);
        Assert.NotNull(read);
        Assert.Equal([bs11, tokyoMX], read.Networks.Select(network => network.ID));
        Assert.Equal([null, "JP"], read.Networks.Select(network => network.CountryOfOrigin));

        Assert.True(orderings.RemoveOrdering(global.ID));
        Reload();

        Assert.Empty(studios.GetNetworks(global.ID));
        Assert.NotNull(studios.GetNetwork(tokyoMX));
        Assert.Superset(new HashSet<MetadataGuid> { tokyoMX, bs11 }, studios.RemoveOrphaned(_plugin, DateTime.MaxValue).ToHashSet());
        seriesStore.RemoveSeries(seriesID);
    }

    [Fact]
    public void AUsersOrderingsNetworksAndStubsReadBackFromTheDatabase()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var seriesStore = fixture.Services.GetRequiredService<IMetadataSeriesStore>();
        var studios = fixture.Services.GetRequiredService<IMetadataStudioStore>();
        var orderings = fixture.Services.GetRequiredService<IMetadataOrderingService>();
        var networkRows = fixture.Services.GetRequiredService<Metadata_NetworkRepository>();
        var seriesID = ID(MetadataEntityType.Series, "ordering-series-3");
        var tokyoMX = ID(MetadataEntityType.Network, "ordering-network-3");
        var atx = ID(MetadataEntityType.Network, "ordering-network-4");
        var gone = ID(MetadataEntityType.Network, "ordering-network-5");
        var fujiTV = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Network, "987654");
        seriesStore.SaveSeries(new() { ID = seriesID });
        studios.SaveNetworks([new() { ID = tokyoMX, Name = "Tokyo MX" }]);
        var local = orderings.CreateLocalOrdering(new() { SeriesID = seriesID, Name = "My Broadcast Order", Networks = [atx, tokyoMX, gone, fujiTV] });
        orderings.UpdateLocalOrdering(local.ID, new() { SeriesID = seriesID, Name = "My Broadcast Order", Networks = [atx, tokyoMX, fujiTV] });

        Reload();

        var read = orderings.GetOrdering(local.ID);
        Assert.NotNull(read);
        Assert.Equal([atx, tokyoMX, fujiTV], read.Networks.Select(network => network.ID));
        Assert.Equal(["", "Tokyo MX", ""], read.Networks.Select(network => network.Name));
        Assert.Equal([true, false, true], new[] { atx, tokyoMX, fujiTV }.Select(network => networkRows.GetByProviderID(network.Source, network.ID)!.IsStub));

        // The purge keeps the linked stub and drops the unlinked one; a provider's save fills the stub in.
        var purged = studios.RemoveOrphaned(_plugin, DateTime.MaxValue);
        Assert.Contains(gone, purged);
        Assert.DoesNotContain(atx, purged);
        studios.SaveNetworks([new() { ID = atx, Name = "AT-X" }]);
        Reload();
        Assert.False(networkRows.GetByProviderID(atx.Source, atx.ID)!.IsStub);
        Assert.Equal("AT-X", orderings.GetOrdering(local.ID)!.Networks[0].Name);

        // Deleting the ordering orphans the TMDB stub, which the purge then removes.
        Assert.True(orderings.DeleteLocalOrdering(local.ID));
        Reload();
        Assert.Empty(studios.GetNetworks(local.ID));
        Assert.NotNull(networkRows.GetByProviderID(fujiTV.Source, fujiTV.ID)?.LastOrphanedAt);
        Assert.Contains(fujiTV, studios.RemoveOrphaned(MetadataSource.TMDB, DateTime.MaxValue));
        Assert.Null(networkRows.GetByProviderID(fujiTV.Source, fujiTV.ID));
        studios.RemoveOrphaned(_plugin, DateTime.MaxValue);
        seriesStore.RemoveSeries(seriesID);
    }

    [Fact]
    public void TheStubStepKeepsEveryNetworkWithItsIDAndItsLinks()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var seriesStore = fixture.Services.GetRequiredService<IMetadataSeriesStore>();
        var studios = fixture.Services.GetRequiredService<IMetadataStudioStore>();
        var orderings = fixture.Services.GetRequiredService<IMetadataOrderingService>();
        var networkRows = fixture.Services.GetRequiredService<Metadata_NetworkRepository>();
        var seriesID = ID(MetadataEntityType.Series, "ordering-series-4");
        var tokyoMX = ID(MetadataEntityType.Network, "ordering-network-6");
        var atx = ID(MetadataEntityType.Network, "ordering-network-7");
        seriesStore.SaveSeries(new() { ID = seriesID });
        studios.SaveNetworks([new() { ID = tokyoMX, Name = "Tokyo MX", CountryOfOrigin = "JP" }]);
        var local = orderings.CreateLocalOrdering(new() { SeriesID = seriesID, Name = "Stubbed", Networks = [atx, tokyoMX] });
        const string networks = "SELECT Metadata_NetworkID, Source, ProviderID, Name, LastUpdatedAt, LastOrphanedAt, CountryOfOrigin FROM Metadata_Network ORDER BY Metadata_NetworkID";

        // SQLite rebuilds the table to let a network be a stub, which drops the refresh time and the
        // extra data added after, so those columns are added back; the others change the column in place.
        int[] revisions = fixture.Services.GetRequiredService<DatabaseFactory>().Instance is SQLite ? [180, 181, 182, 183, 184, 214, 230] : [180];
        using (var connection = fixture.OpenConnection())
        {
            var before = Read(connection, networks);
            foreach (var step in SchemaSteps.Get(fixture, revisions))
                Execute(connection, step.Command!);

            Assert.Equal(before, Read(connection, networks));
        }

        Reload();
        Assert.Equal([atx, tokyoMX], orderings.GetOrdering(local.ID)!.Networks.Select(network => network.ID));
        Assert.True(networkRows.GetByProviderID(atx.Source, atx.ID)!.IsStub);
        var kept = networkRows.GetAll().Max(network => network.Metadata_NetworkID);
        studios.SaveNetworks([new() { ID = ID(MetadataEntityType.Network, "ordering-network-8"), Name = "BS11" }]);
        Assert.True(networkRows.GetByProviderID(_plugin, "ordering-network-8")!.Metadata_NetworkID > kept);

        Assert.True(orderings.DeleteLocalOrdering(local.ID));
        studios.RemoveOrphaned(_plugin, DateTime.MaxValue);
        seriesStore.RemoveSeries(seriesID);
    }

    [Fact]
    public void TheTmdbChoiceStepTurnsTheChosenEpisodeGroupIntoAnOrderingID()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var step = SchemaSteps.GetSql(fixture, 103);

        // The step runs on a copy of the columns it reads and writes, since
        // the migrated table no longer has the episode group column.
        const string Table = "TMDB_Show_ChoiceStepTest";
        using var connection = fixture.OpenConnection();
        Execute(connection, $"CREATE TABLE {Table} (TmdbShowID INT NOT NULL, PreferredAlternateOrderingID VARCHAR(64) NULL, PreferredOrderingID VARCHAR(256) NULL)");
        try
        {
            Execute(connection, $"INSERT INTO {Table} (TmdbShowID, PreferredAlternateOrderingID) VALUES (1, '5f0c1a2b3c4d5e6f7a8b9c0d')");
            Execute(connection, $"INSERT INTO {Table} (TmdbShowID, PreferredAlternateOrderingID) VALUES (2, '2')");
            Execute(connection, $"INSERT INTO {Table} (TmdbShowID, PreferredAlternateOrderingID) VALUES (3, '')");
            Execute(connection, $"INSERT INTO {Table} (TmdbShowID, PreferredAlternateOrderingID) VALUES (4, NULL)");

            Execute(connection, step.Replace("TMDB_Show", Table, StringComparison.Ordinal));

            Assert.Equal("tmdb://ordering/5f0c1a2b3c4d5e6f7a8b9c0d", Scalar(connection, $"SELECT PreferredOrderingID FROM {Table} WHERE TmdbShowID = 1"));
            Assert.Null(Scalar(connection, $"SELECT PreferredOrderingID FROM {Table} WHERE TmdbShowID = 2"));
            Assert.Null(Scalar(connection, $"SELECT PreferredOrderingID FROM {Table} WHERE TmdbShowID = 3"));
            Assert.Null(Scalar(connection, $"SELECT PreferredOrderingID FROM {Table} WHERE TmdbShowID = 4"));
        }
        finally
        {
            Execute(connection, $"DROP TABLE {Table}");
        }
    }
}
