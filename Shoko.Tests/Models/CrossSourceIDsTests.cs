using System;
using System.Linq;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers <see cref="ISeries.CrossSourceIDs"/>, <see cref="IMovie.CrossSourceIDs"/> and
/// <see cref="IEpisode.CrossSourceIDs"/> on the core's entries, which are read off the IDs each
/// entry already carries, and <see cref="CrossSourceIDExtensions"/>, which picks one source's.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class CrossSourceIDsTests
{
    #region Helpers

    private static string[] Texts(IMetadata entry)
        => entry switch
        {
            ISeries series => [.. series.CrossSourceIDs.Select(id => id.ToString())],
            IMovie movie => [.. movie.CrossSourceIDs.Select(id => id.ToString())],
            IEpisode episode => [.. episode.CrossSourceIDs.Select(id => id.ToString())],
            _ => throw new ArgumentException("Not a series, movie or episode.", nameof(entry)),
        };

    #endregion

    #region AniDB

    [Fact]
    public void AnAnidbAnimeListsTheOtherIDsItCarriesForTheSameAnime()
    {
        using var scope = new RepoFactoryScope()
            .With<CrossRef_AniDB_MALRepository, int, CrossRef_AniDB_MAL>(xref => xref.CrossRef_AniDB_MALID,
            [
                new() { CrossRef_AniDB_MALID = 1, AnimeID = 1, MALID = 100 },
                new() { CrossRef_AniDB_MALID = 2, AnimeID = 1, MALID = 101 },
                new() { CrossRef_AniDB_MALID = 3, AnimeID = 1, MALID = 100 },
                new() { CrossRef_AniDB_MALID = 4, AnimeID = 2, MALID = 200 },
            ])
            .With<AniDB_ResourceRepository, int, AniDB_Resource>(row => row.AniDB_ResourceID,
            [
                new() { AniDB_ResourceID = 1, AnimeID = 1, ResourceType = ResourceLinkType.TMDB, Identifiers = ["42", "tv"], Urls = [] },
                new() { AniDB_ResourceID = 2, AnimeID = 1, ResourceType = ResourceLinkType.Syoboi, Identifiers = ["7"], Urls = [] },
                new() { AniDB_ResourceID = 3, AnimeID = 2, ResourceType = ResourceLinkType.IMDb, Identifiers = ["tt0000002"], Urls = [] },
            ]);
        var anime = new AniDB_Anime { AnimeID = 1 };

        // The MAL IDs, then what the anime's resources name on TMDB and IMDb.
        Assert.Equal(["mal://series/100", "mal://series/101", "tmdb://series/42"], Texts(anime));
    }

    #endregion

    #region Extensions

    [Fact]
    public void OneSourcesIDsArePickedInOrder()
    {
        var imdb = MetadataSource.Parse("imdb");
        var first = new MetadataGuid(imdb, MetadataEntityType.Series, "tt0000001");
        var second = new MetadataGuid(imdb, MetadataEntityType.Series, "tt0000002");
        var other = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "7");
        var series = new Mock<ISeries>();
        series.Setup(item => item.CrossSourceIDs).Returns([first, other, second]);
        var movie = new Mock<IMovie>();
        movie.Setup(item => item.CrossSourceIDs).Returns([other]);

        Assert.Equal([first, second], series.Object.GetCrossSourceIDs(imdb));
        Assert.Equal([other], series.Object.GetCrossSourceIDs(TestSources.Plugin));
        Assert.Empty(movie.Object.GetCrossSourceIDs(imdb));
        Assert.Throws<ArgumentNullException>(() => series.Object.GetCrossSourceIDs(null!));
        Assert.Throws<ArgumentNullException>(() => ((ISeries)null!).GetCrossSourceIDs(imdb));
    }

    #endregion
}
