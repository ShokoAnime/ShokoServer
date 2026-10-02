using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Repositories.Direct.TMDB.Optional;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Seeds rows in TMDB's child tables for shows, movies and collections that
/// have no row of their own, and checks the leftover pass the daily orphan
/// purge runs removes them and nothing else.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class TmdbLeftoverPurgeTests(DatabaseMigrationFixture fixture)
{
    #region Leftovers

    [Fact]
    public async Task RowsForEntriesThatAreGoneAreRemovedAndTheRestKept()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var shows = fixture.Services.GetRequiredService<TMDB_ShowRepository>();
        var episodes = fixture.Services.GetRequiredService<TMDB_EpisodeRepository>();
        var movies = fixture.Services.GetRequiredService<TMDB_MovieRepository>();
        var memberships = fixture.Services.GetRequiredService<TMDB_Collection_MovieRepository>();
        var updater = fixture.Services.GetRequiredService<TmdbMetadataUpdater>();

        // An episode of a show with no row, and one of a show that has one.
        var leftoverEpisode = new TMDB_Episode(987_811) { TmdbShowID = 987_801, TmdbSeasonID = 987_821, SeasonNumber = 1, EpisodeNumber = 1 };
        var keptShow = new TMDB_Show(987_802);
        var keptEpisode = new TMDB_Episode(987_812) { TmdbShowID = 987_802, TmdbSeasonID = 987_822, SeasonNumber = 1, EpisodeNumber = 1 };
        // A membership naming a movie with no row, and one naming a stored
        // movie in a collection with no row.
        var leftoverMovieMembership = new TMDB_Collection_Movie(987_831, 987_841, 1);
        var keptMovie = new TMDB_Movie(987_842);
        var leftoverCollectionMembership = new TMDB_Collection_Movie(987_832, 987_842, 1);
        episodes.Save(leftoverEpisode);
        shows.Save(keptShow);
        episodes.Save(keptEpisode);
        movies.Save(keptMovie);
        memberships.Save(leftoverMovieMembership);
        memberships.Save(leftoverCollectionMembership);
        try
        {
            var removed = await updater.PurgeLeftovers(TestContext.Current.CancellationToken);

            Assert.True(removed >= 2, $"Only {removed} leftovers were removed.");
            Assert.Empty(episodes.GetByTmdbShowID(987_801));
            Assert.Single(episodes.GetByTmdbShowID(987_802));
            Assert.NotNull(shows.GetByTmdbShowID(987_802));
            Assert.Empty(memberships.GetByTmdbCollectionID(987_831));
            Assert.Empty(memberships.GetByTmdbCollectionID(987_832));
            Assert.NotNull(movies.GetByTmdbMovieID(987_842));

            // Nothing is left to remove the second time.
            Assert.Equal(0, await updater.PurgeLeftovers(TestContext.Current.CancellationToken));
        }
        finally
        {
            episodes.Delete(episodes.GetByTmdbShowID(987_801));
            episodes.Delete(keptEpisode);
            shows.Delete(keptShow);
            movies.Delete(keptMovie);
            memberships.Delete(memberships.GetByTmdbCollectionID(987_831));
            memberships.Delete(memberships.GetByTmdbCollectionID(987_832));
        }
    }

    #endregion
}
