using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Databases;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Services;
using Shoko.TestData.Schema;
using Xunit;
using static Shoko.IntegrationTests.Sql;

namespace Shoko.IntegrationTests;

/// <summary>
/// Records refresh times through the core's refresh state onto the stored
/// entries' rows in the migrated database and reads them back, and runs the
/// steps that move the old <c>Metadata_Refresh</c> rows onto the entries.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MetadataRefreshStateRoundTripTests(DatabaseMigrationFixture fixture)
{
    #region Helpers

    private static readonly MetadataSource _plugin = TestSources.Plugin;

    private static MetadataGuid ID(MetadataEntityType entityType, string id)
        => new(_plugin, entityType, id);

    /// <summary>
    /// Throws the caches away and reads the stored entries again from the database.
    /// </summary>
    private void Reload()
    {
        var services = fixture.Services;
        foreach (var repository in new ICachedRepository[]
        {
            services.GetRequiredService<Metadata_SeriesRepository>(),
            services.GetRequiredService<Metadata_MovieRepository>(),
            services.GetRequiredService<Metadata_CollectionRepository>(),
            services.GetRequiredService<Metadata_CreatorRepository>(),
            services.GetRequiredService<Metadata_StudioRepository>(),
        })
            repository.Populate(displayName: false);
    }

    #endregion

    #region Tests

    [Fact]
    public void ARecordedRefreshIsKeptOnTheRowUntilItIsPurged()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var state = services.GetRequiredService<IMetadataRefreshState>();
        var seriesStore = services.GetRequiredService<IMetadataSeriesStore>();
        var movieStore = services.GetRequiredService<IMetadataMovieStore>();
        var collectionStore = services.GetRequiredService<IMetadataCollectionStore>();
        var people = services.GetRequiredService<IMetadataPeopleStore>();
        var studios = services.GetRequiredService<IMetadataStudioStore>();
        var series = ID(MetadataEntityType.Series, new string('r', MetadataGuid.MaxIDLength));
        var movie = ID(MetadataEntityType.Movie, "refresh-movie-1");
        var collection = ID(MetadataEntityType.Collection, "refresh-collection-1");
        var creator = ID(MetadataEntityType.Creator, "refresh-creator-1");
        var studio = ID(MetadataEntityType.Studio, "refresh-studio-1");
        var first = new DateTime(2026, 9, 24, 12, 30, 15, DateTimeKind.Local);
        var second = first.AddHours(3);
        seriesStore.SaveSeries(new() { ID = series });
        movieStore.SaveMovie(new() { ID = movie });
        collectionStore.SaveCollection(new() { ID = collection, Members = [movie] });
        people.SaveCreators([new() { ID = creator, Name = "Kana" }]);
        studios.SaveStudios([new() { ID = studio, Name = "Sunrise" }]);
        try
        {
            Assert.Null(state.GetLastRefreshedAt(series));
            foreach (var entry in new[] { series, movie, collection, creator, studio })
                Assert.True(state.RecordRefresh(entry, first));
            Assert.False(state.RecordRefresh(ID(MetadataEntityType.Series, "refresh-missing"), first));
            Reload();

            foreach (var entry in new[] { series, movie, collection, creator, studio })
                Assert.Equal(first.ToUniversalTime(), state.GetLastRefreshedAt(entry));
            Assert.Equal(first, services.GetRequiredService<Metadata_SeriesRepository>().GetByProviderID(_plugin, series.ID)!.LastRefreshedAt);

            // A save by the source keeps the time, and a later refresh replaces it.
            seriesStore.SaveSeries(new() { ID = series, Rating = 5 });
            people.SaveCreators([new() { ID = creator, Name = "Kana Hanazawa" }]);
            state.RecordRefresh(movie, second.ToUniversalTime());
            Reload();
            Assert.Equal(first.ToUniversalTime(), state.GetLastRefreshedAt(series));
            Assert.Equal(first.ToUniversalTime(), state.GetLastRefreshedAt(creator));
            Assert.Equal(second.ToUniversalTime(), state.GetLastRefreshedAt(movie));

            // The time goes with the row, so a series stored again starts over.
            seriesStore.RemoveSeries(series);
            seriesStore.SaveSeries(new() { ID = series });
            Reload();
            Assert.Null(state.GetLastRefreshedAt(series));
        }
        finally
        {
            collectionStore.RemoveCollection(collection);
            movieStore.RemoveMovie(movie);
            seriesStore.RemoveSeries(series);
            people.RemoveOrphaned(_plugin, DateTime.MaxValue);
            studios.RemoveOrphaned(_plugin, DateTime.MaxValue);
        }
    }

    [Fact]
    public void TheStepsMoveTheOldRefreshTimesOntoTheEntriesAndDropTheRest()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var seriesStore = services.GetRequiredService<IMetadataSeriesStore>();
        var movieStore = services.GetRequiredService<IMetadataMovieStore>();
        var people = services.GetRequiredService<IMetadataPeopleStore>();
        var state = services.GetRequiredService<IMetadataRefreshState>();
        var series = ID(MetadataEntityType.Series, "refresh-copy-series");
        var movie = ID(MetadataEntityType.Movie, "refresh-copy-movie");
        var creator = ID(MetadataEntityType.Creator, "refresh-copy-creator");
        seriesStore.SaveSeries(new() { ID = series });
        movieStore.SaveMovie(new() { ID = movie });
        people.SaveCreators([new() { ID = creator, Name = "Kana" }]);
        var isSQLite = services.GetRequiredService<DatabaseFactory>().Instance is SQLite;
        using var connection = fixture.OpenConnection();
        try
        {
            // The table as the release before had it, with an entry of each kind and one stored nowhere.
            Execute(connection, SchemaSteps.GetSql(fixture, 88));
            Execute(connection, SchemaSteps.GetSql(fixture, 89));
            var source = Scalar(connection, $"SELECT Source FROM Metadata_Series WHERE ProviderID = '{series.ID}'");
            foreach (var (entityType, id, at) in new[] { (2, series.ID, "12:30:15"), (5, movie.ID, "13:30:15"), (9, creator.ID, "14:30:15"), (2, "refresh-copy-gone", "15:30:15") })
                Execute(
                    connection,
                    $"INSERT INTO Metadata_Refresh (Source, EntityType, ProviderID, LastRefreshedAt) VALUES ({source}, {entityType}, '{id}', '2026-09-24T{at}')"
                );

            var first = isSQLite ? 215 : 216;
            foreach (var step in SchemaSteps.Get(fixture, first, first + 1, first + 2, first + 3, first + 4, first + 5, first + 6, first + 7))
                Execute(connection, step.Command!);

            Assert.False(SchemaSnapshot.Read(connection, fixture.Backend).Tables.ContainsKey("Metadata_Refresh"));
            Reload();
            var day = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Local);
            Assert.Equal(day.AddHours(12.5).AddSeconds(15).ToUniversalTime(), state.GetLastRefreshedAt(series));
            Assert.Equal(day.AddHours(13.5).AddSeconds(15).ToUniversalTime(), state.GetLastRefreshedAt(movie));
            Assert.Equal(day.AddHours(14.5).AddSeconds(15).ToUniversalTime(), state.GetLastRefreshedAt(creator));
        }
        finally
        {
            if (SchemaSnapshot.Read(connection, fixture.Backend).Tables.ContainsKey("Metadata_Refresh"))
                Execute(connection, "DROP TABLE Metadata_Refresh");
            movieStore.RemoveMovie(movie);
            seriesStore.RemoveSeries(series);
            people.RemoveOrphaned(_plugin, DateTime.MaxValue);
        }
    }

    #endregion
}
