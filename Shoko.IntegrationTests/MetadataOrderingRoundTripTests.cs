using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.TMDB;
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
            services.GetRequiredService<TMDB_ShowRepository>(),
            services.GetRequiredService<TMDB_EpisodeRepository>(),
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
    public void TmdbChoicesAndHiddenEpisodesAreKeptOnTmdbsOwnRows()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var shows = fixture.Services.GetRequiredService<TMDB_ShowRepository>();
        var episodes = fixture.Services.GetRequiredService<TMDB_EpisodeRepository>();
        var groups = fixture.Services.GetRequiredService<TMDB_AlternateOrderingRepository>();
        var orderings = fixture.Services.GetRequiredService<IMetadataOrderingService>();
        var group = new TMDB_AlternateOrdering("5f0c1a2b3c4d5e6f7a8b9c0d") { TmdbShowID = 987_701, EnglishTitle = "DVD Order", Type = OrderingType.DVD };
        shows.Save(new TMDB_Show(987_701));
        shows.Save(new TMDB_Show(987_702));
        episodes.Save(new TMDB_Episode(987_703) { TmdbShowID = 987_701, TmdbSeasonID = 987_704, SeasonNumber = 1, EpisodeNumber = 1 });
        groups.Save(group);
        var showID = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "987701");
        var episodeID = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Episode, "987703");
        var groupID = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Ordering, group.TmdbEpisodeGroupCollectionID);
        try
        {
            // What the TMDB endpoints write goes through the ordering service.
            Assert.True(orderings.SetPreferredOrdering(showID, groupID));
            Assert.True(orderings.SetEpisodeHidden(episodeID, true));
            Reload();

            var show = shows.GetByTmdbShowID(987_701)!;
            Assert.Equal(groupID, show.PreferredOrderingID);
            Assert.Null(shows.GetByTmdbShowID(987_702)!.PreferredOrderingID);
            Assert.Equal(groupID, orderings.GetPreferredOrdering(show).ID);
            Assert.Equal(group.TmdbEpisodeGroupCollectionID, show.PreferredAlternateOrderingID);
            Assert.True(orderings.IsEpisodeHidden(episodeID));
            Assert.True(episodes.GetByTmdbEpisodeID(987_703)!.IsHidden);
            using (var connection = fixture.OpenConnection())
                Assert.Equal(groupID.ToString(), Scalar(connection, "SELECT PreferredOrderingID FROM TMDB_Show WHERE TmdbShowID = 987701"));

            var all = orderings.GetOrderings(show);
            Assert.Equal([new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Ordering, "987701"), groupID], all.Select(ordering => ordering.ID));
            Assert.All(all, ordering => Assert.IsAssignableFrom<ITmdbShowOrderingInformation>(ordering));
            Assert.False(all[0].IsPreferred);
            Assert.True(all[1].IsPreferred);

            Assert.True(orderings.SetPreferredOrdering(showID, null));
            Assert.True(orderings.SetEpisodeHidden(episodeID, false));
            Reload();

            Assert.Null(shows.GetByTmdbShowID(987_701)!.PreferredOrderingID);
            Assert.Null(shows.GetByTmdbShowID(987_701)!.PreferredAlternateOrderingID);
            Assert.False(episodes.GetByTmdbEpisodeID(987_703)!.IsHidden);
        }
        finally
        {
            orderings.SetPreferredOrdering(showID, null);
            orderings.SetEpisodeHidden(episodeID, false);
            groups.Delete(group);
            episodes.Delete(episodes.GetByTmdbEpisodeID(987_703)!);
            shows.Delete(shows.GetByTmdbShowID(987_701)!);
            shows.Delete(shows.GetByTmdbShowID(987_702)!);
        }
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
