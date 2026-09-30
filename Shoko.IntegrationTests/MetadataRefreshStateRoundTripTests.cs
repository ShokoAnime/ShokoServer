using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Records refresh times through the core's refresh state into the migrated
/// database and reads them back from it, so the <c>Metadata_Refresh</c>
/// mapping, its column types and its unique index are checked on each
/// backend.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MetadataRefreshStateRoundTripTests(DatabaseMigrationFixture fixture)
{
    private static readonly MetadataSource _plugin = TestSources.Plugin;

    /// <summary>
    /// Throws the cache away and reads the table again from the database.
    /// </summary>
    private void Reload()
        => fixture.Services.GetRequiredService<Metadata_RefreshRepository>().Populate(displayName: false);

    [Fact]
    public void ARecordedRefreshReadsBackFromTheDatabase()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var state = fixture.Services.GetRequiredService<IMetadataRefreshState>();
        var series = new MetadataGuid(_plugin, MetadataEntityType.Series, new string('r', MetadataGuid.MaxIDLength));
        var movie = new MetadataGuid(_plugin, MetadataEntityType.Movie, new string('r', MetadataGuid.MaxIDLength));
        var first = new DateTime(2026, 9, 24, 12, 30, 15);
        var second = first.AddHours(3);

        state.RecordRefresh(series, first);
        state.RecordRefresh(movie, first);
        Reload();

        Assert.Equal(first, state.GetLastRefreshedAt(series));
        Assert.Equal(first, state.GetLastRefreshedAt(movie));

        // A second refresh updates the row rather than adding one, which the
        // unique index would refuse.
        state.RecordRefresh(series, second);
        Reload();

        Assert.Equal(second, state.GetLastRefreshedAt(series));
        Assert.Equal(first, state.GetLastRefreshedAt(movie));
        var row = fixture.Services.GetRequiredService<Metadata_RefreshRepository>().GetByEntry(series);
        Assert.NotNull(row);
        Assert.Equal(series, row.EntryID);

        Assert.True(state.Forget(series));
        Assert.False(state.Forget(series));
        Reload();

        Assert.Null(state.GetLastRefreshedAt(series));
        Assert.Equal(first, state.GetLastRefreshedAt(movie));
        Assert.True(state.Forget(movie));
    }

    [Fact]
    public void ATmdbEntrysRefreshTimeIsReadFromTmdbsOwnTables()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var state = fixture.Services.GetRequiredService<IMetadataRefreshState>();
        var shows = fixture.Services.GetRequiredService<TMDB_ShowRepository>();
        var movies = fixture.Services.GetRequiredService<TMDB_MovieRepository>();
        var collections = fixture.Services.GetRequiredService<TMDB_CollectionRepository>();
        var updatedAt = new DateTime(2026, 9, 24, 12, 30, 15);
        var show = new TMDB_Show(987_601) { LastUpdatedAt = updatedAt };
        var movie = new TMDB_Movie(987_602);
        var collection = new TMDB_Collection(987_603) { LastUpdatedAt = updatedAt };
        shows.Save(show);
        movies.Save(movie);
        collections.Save(collection);
        var showID = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "987601");
        var movieID = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Movie, "987602");
        var collectionID = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Collection, "987603");

        try
        {
            Assert.Equal(updatedAt, state.GetLastRefreshedAt(showID));
            Assert.Equal(updatedAt, state.GetLastRefreshedAt(collectionID));

            // Never updated after it was added, so never refreshed.
            Assert.Null(state.GetLastRefreshedAt(movieID));
            Assert.Null(state.GetLastRefreshedAt(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "987604")));

            // TMDB keeps its own times, so nothing is written for it.
            state.RecordRefresh(movieID, updatedAt);
            Reload();
            Assert.Null(fixture.Services.GetRequiredService<Metadata_RefreshRepository>().GetByEntry(movieID));
            Assert.Null(state.GetLastRefreshedAt(movieID));
            Assert.False(state.Forget(showID));
            Assert.Equal(updatedAt, state.GetLastRefreshedAt(showID));
        }
        finally
        {
            shows.Delete(show);
            movies.Delete(movie);
            collections.Delete(collection);
        }
    }
}
