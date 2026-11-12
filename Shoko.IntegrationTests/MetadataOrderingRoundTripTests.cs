using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
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

    private static IReadOnlyList<ITitle> Named(string name)
        => [new TitleStub { Source = _plugin, Language = TitleLanguage.English, LanguageCode = "en", Value = name, Type = TitleType.Main }];

    private static IReadOnlyList<IText> Described(string overview)
        => [new TextStub { Source = _plugin, Language = TitleLanguage.English, LanguageCode = "en", Value = overview }];

    /// <summary>
    /// Throws the caches away and reads every ordering table and the texts
    /// again from the database.
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
        services.GetRequiredService<TextCache>().Populate(false, TestContext.Current.CancellationToken);
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
            Titles = Named("DVD Order"),
            Overviews = Described(longText),
            Type = OrderingType.DVD,
            Groups =
            [
                new()
                {
                    ID = ID(MetadataEntityType.Season, "ordering-group-1"),
                    Titles = Named("Disc 1"),
                    Overviews = Described("The first disc."),
                    Episodes = [episode(2), episode(1)],
                },
                new() { ID = ID(MetadataEntityType.Season, longID), Titles = Named(longText), Episodes = [episode(4), episode(1)] },
            ],
        });
        var local = orderings.CreateLocalOrdering(new()
        {
            SeriesID = seriesID,
            Titles = MetadataOrderingService.UserTitles("Mine"),
            Groups =
            [
                new() { Titles = MetadataOrderingService.UserTitles("Extras"), IsSpecial = true, Episodes = [episode(4)] },
                new() { Titles = MetadataOrderingService.UserTitles("All"), Episodes = [episode(3), episode(2), episode(1)] },
            ],
        });
        // Replacing the global ordering updates its rows in place and drops the ones left out.
        global = orderings.SaveOrdering(new()
        {
            ID = global.ID,
            SeriesID = seriesID,
            Titles = Named("DVD Order"),
            Overviews = Described(longText),
            Type = OrderingType.DVD,
            Groups =
            [
                new() { ID = ID(MetadataEntityType.Season, longID), Titles = Named(longText), Episodes = [episode(1), episode(4)] },
                new()
                {
                    ID = ID(MetadataEntityType.Season, "ordering-group-1"),
                    Titles = Named("Disc 1"),
                    Overviews = Described("The first disc."),
                    IsSpecial = true,
                    Episodes = [episode(2)],
                },
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
        Assert.Equal("DVD Order", readGlobal.Title);
        Assert.Equal(longText, readGlobal.DefaultOverview?.Value);
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
        Assert.Equal("Mine", readLocal.Title);
        Assert.Equal(["Extras", "All"], readLocal.Seasons.Select(group => group.Title));
        Assert.All(readLocal.Titles, title => Assert.Equal(MetadataSource.User, title.Source));

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
        var texts = fixture.Services.GetRequiredService<TextCache>();
        Assert.All(new[] { global.ID, local.ID, ID(MetadataEntityType.Season, longID) }, entry => Assert.Empty(texts.GetRows(entry)));
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
        var local = orderings.CreateLocalOrdering(new() { SeriesID = seriesID, Networks = [atx, tokyoMX, gone, fujiTV] });
        orderings.UpdateLocalOrdering(local.ID, new() { SeriesID = seriesID, Networks = [atx, tokyoMX, fujiTV] });

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
}
