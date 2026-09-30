using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.CrossReference.Embedded;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers what the cross-reference store refuses to write: a series or film
/// link to nothing, and a link to an ID its source never gives, such as a
/// TMDB 0. An episode may still be linked to nothing.
/// </summary>
public class MetadataLinkRefusalTests
{
    #region Helpers

    private const int AnimeID = 100;

    private static CancellationToken Token
        => TestContext.Current.CancellationToken;

    private static MetadataGuid Tmdb(MetadataEntityType entityType, string id)
        => new(MetadataSource.TMDB, entityType, id);

    private static MetadataSeriesLinkData SeriesLink(MetadataSource source, MetadataGuid? providerID)
        => new() { Source = source, AnidbAnimeID = AnimeID, ProviderID = providerID };

    private static MetadataMovieLinkData MovieLink(MetadataSource source, MetadataGuid? providerID)
        => new() { Source = source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 1001, ProviderID = providerID };

    private static MetadataEpisodeLinkData EpisodeLink(
        MetadataSource source,
        MetadataGuid? providerID,
        MetadataGuid? parentID = null,
        MetadataGuid? seasonID = null
    )
        => new()
        {
            Source = source,
            AnidbAnimeID = AnimeID,
            AnidbEpisodeID = 1001,
            ProviderID = providerID,
            ProviderParentID = parentID,
            SeasonID = seasonID,
        };

    #endregion

    #region Links to nothing

    [Fact]
    public async Task AFilmLinkToNothingIsRefused()
    {
        var links = new WritableLinkStore();

        await Assert.ThrowsAsync<ArgumentException>(() => links.Store.MergeMovieLinks([MovieLink(TestSources.Plugin, null)], cancellationToken: Token));

        Assert.Empty(links.Movies.GetAll());
    }

    [Fact]
    public async Task AnEpisodeLinkToNothingIsStillKept()
    {
        var links = new WritableLinkStore(new TmdbLinkIDRule());

        await links.Store.MergeEpisodeLinks([EpisodeLink(MetadataSource.TMDB, null)], cancellationToken: Token);

        var stored = Assert.Single(links.Episodes.GetAll());
        Assert.True(stored.IsUnlinked);
    }

    [Fact]
    public async Task OneRefusedLinkLeavesTheWholeWriteUndone()
    {
        var links = new WritableLinkStore();

        await Assert.ThrowsAsync<ArgumentException>(() => links.Store.MergeSeriesLinks([
            SeriesLink(TestSources.Plugin, new(TestSources.Plugin, MetadataEntityType.Series, "21")),
            SeriesLink(TestSources.Plugin, null),
        ], cancellationToken: Token));

        Assert.Empty(links.Series.GetAll());
    }

    [Fact]
    public void TheServersOwnWritesRefuseALinkToNothingToo()
    {
        var links = new WritableLinkStore(new TmdbLinkIDRule());

        Assert.Throws<ArgumentException>(() => links.Store.AddSeriesLink(MetadataSource.TMDB, AnimeID, null, MatchRating.UserVerified));
        Assert.Throws<ArgumentException>(() => links.Store.AddMovieLink(MetadataSource.TMDB, AnimeID, 1001, null, MatchRating.UserVerified));

        Assert.Empty(links.Series.GetAll());
        Assert.Empty(links.Movies.GetAll());
    }

    [Fact]
    public void TmdbsOwnTablesCannotStoreAShowOrMovieZero()
    {
        // TMDB's view turns a 0 into no entry, which the store refuses.
        var links = new WritableLinkStore(new TmdbLinkIDRule());
        var shows = new CrossRef_AniDB_TMDB_ShowRepository(links.Series, links.Store);
        var movies = new CrossRef_AniDB_TMDB_MovieRepository(links.Movies, links.Store);

        Assert.Throws<ArgumentException>(() => shows.Save(new CrossRef_AniDB_TMDB_Show(AnimeID, 0)));
        Assert.Throws<ArgumentException>(() => movies.Save(new CrossRef_AniDB_TMDB_Movie(1001, AnimeID, 0)));
        shows.Save(new CrossRef_AniDB_TMDB_Show(AnimeID, 42));

        Assert.Equal(["42"], links.Series.GetAll().Select(row => row.ProviderID));
        Assert.Empty(links.Movies.GetAll());
    }

    #endregion

    #region Source rules

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("007")]
    [InlineData("abc")]
    [InlineData("2147483648")]
    public async Task ATmdbShowOrMovieOrEpisodeThatIsNotAPositiveNumberIsRefused(string id)
    {
        var links = new WritableLinkStore(new TmdbLinkIDRule());
        var store = links.Store;
        var show = SeriesLink(MetadataSource.TMDB, Tmdb(MetadataEntityType.Series, id));
        var wholeMovie = SeriesLink(MetadataSource.TMDB, Tmdb(MetadataEntityType.Movie, id));
        var movie = MovieLink(MetadataSource.TMDB, Tmdb(MetadataEntityType.Movie, id));
        var episode = EpisodeLink(MetadataSource.TMDB, Tmdb(MetadataEntityType.Episode, id));
        var episodeInShow = EpisodeLink(MetadataSource.TMDB, Tmdb(MetadataEntityType.Episode, "7"), Tmdb(MetadataEntityType.Series, id));

        await Assert.ThrowsAsync<ArgumentException>(() => store.MergeSeriesLinks([show], cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentException>(() => store.MergeSeriesLinks([wholeMovie], cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentException>(() => store.MergeMovieLinks([movie], cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentException>(() => store.MergeEpisodeLinks([episode], cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentException>(() => store.MergeEpisodeLinks([episodeInShow], cancellationToken: Token));

        Assert.Empty(links.Series.GetAll());
        Assert.Empty(links.Movies.GetAll());
        Assert.Empty(links.Episodes.GetAll());
    }

    [Fact]
    public async Task ATmdbEpisodeInAnEpisodeGroupIsStored()
    {
        var links = new WritableLinkStore(new TmdbLinkIDRule());

        // An alternate ordering's season is an episode group, named by text.
        var episodeGroup = Tmdb(MetadataEntityType.Season, "5acf93e60e0a26346d0000ce");
        await links.Store.MergeEpisodeLinks(
            [EpisodeLink(MetadataSource.TMDB, Tmdb(MetadataEntityType.Episode, "44"), Tmdb(MetadataEntityType.Series, "42"), episodeGroup)],
            cancellationToken: Token
        );

        Assert.Equal("44", Assert.Single(links.Episodes.GetAll()).ProviderID);
    }

    [Fact]
    public async Task ASourceWithNoRuleTakesAnyID()
    {
        // A 0 may be a real ID on a source that says nothing about its IDs.
        var links = new WritableLinkStore(new TmdbLinkIDRule());
        var zero = new MetadataGuid(TestSources.AniList, MetadataEntityType.Series, "0");

        await links.Store.MergeSeriesLinks([SeriesLink(TestSources.AniList, zero)], cancellationToken: Token);

        Assert.Equal(["0"], links.Series.GetAll().Select(row => row.ProviderID));
    }

    #endregion
}
