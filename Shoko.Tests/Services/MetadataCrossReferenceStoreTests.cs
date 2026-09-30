using System;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="MetadataCrossReferenceStore"/>: the bulk reads of every link
/// at a level in a stable order, the entry each level's links name by kind, and
/// the refusal of a link naming another source or kind before anything is written.
/// </summary>
public class MetadataCrossReferenceStoreTests
{
    private static MetadataCrossReferenceStore Store()
        => new(
            CachedRepo.Build<CrossRef_AniDB_Metadata_SeriesRepository, int, CrossRef_AniDB_Metadata_Series>(
                x => x.CrossRef_AniDB_Metadata_SeriesID,
                new() { CrossRef_AniDB_Metadata_SeriesID = 1, Source = MetadataSource.TMDB, AnidbAnimeID = 20, ProviderID = "b", Ordering = 1 },
                new() { CrossRef_AniDB_Metadata_SeriesID = 2, Source = TestSources.AniList, AnidbAnimeID = 10, ProviderID = "c" },
                new() { CrossRef_AniDB_Metadata_SeriesID = 3, Source = MetadataSource.TMDB, AnidbAnimeID = 20, ProviderID = "a" },
                new() { CrossRef_AniDB_Metadata_SeriesID = 4, Source = MetadataSource.TMDB, AnidbAnimeID = 10, ProviderID = "d" },
                new()
                {
                    CrossRef_AniDB_Metadata_SeriesID = 5,
                    Source = TestSources.AniList,
                    AnidbAnimeID = 30,
                    ProviderID = "f",
                    ProviderType = MetadataEntityType.Movie,
                }
            ),
            CachedRepo.Build<CrossRef_AniDB_Metadata_MovieRepository, int, CrossRef_AniDB_Metadata_Movie>(
                x => x.CrossRef_AniDB_Metadata_MovieID,
                new CrossRef_AniDB_Metadata_Movie { CrossRef_AniDB_Metadata_MovieID = 1, Source = MetadataSource.TMDB, AnidbAnimeID = 10, AnidbEpisodeID = 101, ProviderID = "m" }
            ),
            CachedRepo.Build<CrossRef_AniDB_Metadata_EpisodeRepository, int, CrossRef_AniDB_Metadata_Episode>(
                x => x.CrossRef_AniDB_Metadata_EpisodeID,
                new() { CrossRef_AniDB_Metadata_EpisodeID = 1, Source = MetadataSource.TMDB, AnidbAnimeID = 10, AnidbEpisodeID = 102, ProviderID = "e2" },
                new() { CrossRef_AniDB_Metadata_EpisodeID = 2, Source = MetadataSource.TMDB, AnidbAnimeID = 10, AnidbEpisodeID = 101, ProviderID = "e1b", Ordering = 1 },
                new() { CrossRef_AniDB_Metadata_EpisodeID = 3, Source = MetadataSource.TMDB, AnidbAnimeID = 10, AnidbEpisodeID = 101, ProviderID = "e1a" },
                new() { CrossRef_AniDB_Metadata_EpisodeID = 4, Source = TestSources.AniList, AnidbAnimeID = 10, AnidbEpisodeID = 101, ProviderID = "x" }
            ),
            CachedRepo.Build<Metadata_EpisodeRepository, int, Metadata_Episode>(row => row.Metadata_EpisodeID)
        );

    [Fact]
    public void EverySeriesLinkComesBackBySourceThenAnimeThenPosition()
    {
        var links = Store().GetAllSeriesLinks();

        Assert.Equal(["d", "a", "b", "c", "f"], links.Select(x => x.ProviderID!.ID));
    }

    [Fact]
    public void OneSourceOnlyGetsItsOwnLinks()
    {
        var store = Store();

        Assert.Equal(["c", "f"], store.GetAllSeriesLinks(TestSources.AniList).Select(x => x.ProviderID!.ID));
        Assert.Empty(store.GetAllMovieLinks(TestSources.AniList));
    }

    [Fact]
    public void EveryEpisodeLinkComesBackByEpisodeThenPosition()
    {
        var links = Store().GetAllEpisodeLinks(MetadataSource.TMDB);

        Assert.Equal(["e1a", "e1b", "e2"], links.Select(x => x.ProviderID!.ID));
    }

    [Fact]
    public void TheEpisodeLinksIntoASeriesAreThoseUnderItOrOnItsStoredEpisodes()
    {
        var store = new MetadataCrossReferenceStore(
            CachedRepo.Build<CrossRef_AniDB_Metadata_SeriesRepository, int, CrossRef_AniDB_Metadata_Series>(x => x.CrossRef_AniDB_Metadata_SeriesID),
            CachedRepo.Build<CrossRef_AniDB_Metadata_MovieRepository, int, CrossRef_AniDB_Metadata_Movie>(x => x.CrossRef_AniDB_Metadata_MovieID),
            CachedRepo.Build<CrossRef_AniDB_Metadata_EpisodeRepository, int, CrossRef_AniDB_Metadata_Episode>(
                x => x.CrossRef_AniDB_Metadata_EpisodeID,
                new() { CrossRef_AniDB_Metadata_EpisodeID = 1, Source = TestSources.AniList, AnidbAnimeID = 10, AnidbEpisodeID = 102, ProviderID = "e2", ProviderParentID = "s" },
                new() { CrossRef_AniDB_Metadata_EpisodeID = 2, Source = TestSources.AniList, AnidbAnimeID = 20, AnidbEpisodeID = 201, ProviderID = "e1" },
                new() { CrossRef_AniDB_Metadata_EpisodeID = 3, Source = TestSources.AniList, AnidbAnimeID = 10, AnidbEpisodeID = 101, ProviderID = "e1", ProviderParentID = "s" },
                new() { CrossRef_AniDB_Metadata_EpisodeID = 4, Source = TestSources.AniList, AnidbAnimeID = 10, AnidbEpisodeID = 103, ProviderID = "o", ProviderParentID = "other" },
                new() { CrossRef_AniDB_Metadata_EpisodeID = 5, Source = MetadataSource.TMDB, AnidbAnimeID = 10, AnidbEpisodeID = 104, ProviderID = "e1", ProviderParentID = "s" }
            ),
            CachedRepo.Build<Metadata_EpisodeRepository, int, Metadata_Episode>(
                row => row.Metadata_EpisodeID,
                new Metadata_Episode { Metadata_EpisodeID = 1, Source = TestSources.AniList, ProviderID = "e1", SeriesID = "s" }
            )
        );

        var links = store.GetEpisodeLinksInto(new(TestSources.AniList, MetadataEntityType.Series, "s"));

        Assert.Equal([101, 102, 201], links.Select(x => x.AnidbEpisodeID));
        Assert.Empty(store.GetEpisodeLinksInto(new(TestSources.AniList, MetadataEntityType.Movie, "s")));
    }

    [Fact]
    public void ALinkNamesItsEntryAsTheKindItsLevelStores()
    {
        var store = Store();

        Assert.Equal(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "d"), store.GetSeriesLinks(10, MetadataSource.TMDB)[0].ProviderID);
        Assert.Equal(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Movie, "m"), store.GetMovieLinks(101)[0].ProviderID);
        Assert.Equal(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Episode, "e1a"), store.GetEpisodeLinks(101, MetadataSource.TMDB)[0].ProviderID);
    }

    [Fact]
    public void TheLinksToAnEntryAreFoundAtTheLevelItsKindNames()
    {
        var store = Store();

        Assert.Equal([20], store.GetLinksTo(new(MetadataSource.TMDB, MetadataEntityType.Series, "a")).Select(x => x.AnidbAnimeID));
        Assert.Equal([10], store.GetLinksTo(new(MetadataSource.TMDB, MetadataEntityType.Movie, "m")).Select(x => x.AnidbAnimeID));
        Assert.Empty(store.GetLinksTo(new(MetadataSource.TMDB, MetadataEntityType.Series, "m")));
        Assert.Empty(store.GetLinksTo(new(TestSources.AniList, MetadataEntityType.Series, "a")));
    }

    [Fact]
    public void AFilmClaimingAWholeAnimeReadsBackAsTheFilm()
    {
        var store = Store();
        var film = new MetadataGuid(TestSources.AniList, MetadataEntityType.Movie, "f");

        var link = Assert.Single(store.GetSeriesLinks(30));
        Assert.Equal(film, link.ProviderID);
        Assert.Equal(MetadataEntityType.Series, link.EntityType);
        Assert.Equal([30], store.GetLinksTo(film).Select(x => x.AnidbAnimeID));
        Assert.Empty(store.GetLinksTo(new(TestSources.AniList, MetadataEntityType.Series, "f")));
    }

    [Fact]
    public void ASeriesLevelRowNamesItsEntryByKindAsWellAsByID()
    {
        var series = new MetadataGuid(TestSources.AniList, MetadataEntityType.Series, "123");
        var film = new MetadataGuid(TestSources.AniList, MetadataEntityType.Movie, "123");
        var row = new CrossRef_AniDB_Metadata_Series { Source = TestSources.AniList, AnidbAnimeID = 1, ProviderID = "123" };

        Assert.True(row.Names(series));
        Assert.False(row.Names(film));
        Assert.False(row.Names(null));

        row.ProviderType = MetadataEntityType.Movie;
        Assert.True(row.Names(film));
        Assert.False(row.Names(series));

        Assert.True(new CrossRef_AniDB_Metadata_Series { Source = TestSources.AniList, AnidbAnimeID = 1 }.Names(null));
    }

    [Fact]
    public async Task ALinkNamingAnotherSourceOrKindIsRefusedBeforeAnythingIsWritten()
    {
        var store = Store();
        var cancellationToken = TestContext.Current.CancellationToken;
        var film = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Movie, "m");

        await Assert.ThrowsAsync<ArgumentException>(() => store.MergeSeriesLinks(
            [new() { Source = MetadataSource.TMDB, AnidbAnimeID = 10, ProviderID = new(MetadataSource.TMDB, MetadataEntityType.Episode, "e1") }],
            cancellationToken: cancellationToken
        ));
        await Assert.ThrowsAsync<ArgumentException>(() => store.MergeSeriesLinks(
            [new() { Source = MetadataSource.TMDB, AnidbAnimeID = 10, ProviderID = new(TestSources.AniList, MetadataEntityType.Movie, "m") }],
            cancellationToken: cancellationToken
        ));
        await Assert.ThrowsAsync<ArgumentException>(() => store.MergeMovieLinks(
            [new() { Source = TestSources.AniList, AnidbAnimeID = 10, AnidbEpisodeID = 101, ProviderID = film }],
            cancellationToken: cancellationToken
        ));
        await Assert.ThrowsAsync<ArgumentException>(() => store.MergeEpisodeLinks(
            [new()
            {
                Source = MetadataSource.TMDB,
                AnidbAnimeID = 10,
                AnidbEpisodeID = 101,
                ProviderID = new(MetadataSource.TMDB, MetadataEntityType.Episode, "e3"),
                ProviderParentID = film,
            }],
            cancellationToken: cancellationToken
        ));
        await Assert.ThrowsAsync<ArgumentException>(() => store.MergeEpisodeLinks(
            [new()
            {
                Source = MetadataSource.TMDB,
                AnidbAnimeID = 10,
                AnidbEpisodeID = 101,
                ProviderID = new(MetadataSource.TMDB, MetadataEntityType.Episode, "e3"),
                SeasonID = new(MetadataSource.TMDB, MetadataEntityType.Series, "s1"),
            }],
            cancellationToken: cancellationToken
        ));

        Assert.Equal(5, store.GetAllSeriesLinks().Count);
        Assert.Single(store.GetAllMovieLinks());
        Assert.Equal(4, store.GetAllEpisodeLinks().Count);
    }
}
