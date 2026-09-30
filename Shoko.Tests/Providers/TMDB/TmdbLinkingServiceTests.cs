using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Providers.TMDB;

/// <summary>
/// Covers <see cref="TmdbLinkingService"/> as a shim: each call reaches the
/// generic linking service for the <c>tmdb</c> source, with TMDB's own flags.
/// </summary>
public class TmdbLinkingServiceTests
{
    #region Fixtures

    private const int AnimeID = 100;

    private const int EpisodeID = 11;

    private static readonly MetadataGuid Show = new(MetadataSource.TMDB, MetadataEntityType.Series, "5");

    private static readonly MetadataGuid Movie = new(MetadataSource.TMDB, MetadataEntityType.Movie, "7");

    private sealed record Fixture(
        TmdbLinkingService Service,
        Mock<IMetadataLinkingService> Linking,
        Mock<IMetadataService> Metadata
    );

    private static Fixture Build()
    {
        var linking = new Mock<IMetadataLinkingService>();
        linking.Setup(l => l.MatchEpisodes(
                It.IsAny<int>(),
                It.IsAny<MetadataGuid>(),
                It.IsAny<MetadataGuid?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool?>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync([]);
        linking.Setup(l => l.SetEpisodeLink(
                It.IsAny<MetadataSource>(),
                It.IsAny<int>(),
                It.IsAny<MetadataGuid?>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<MetadataGuid?>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(true);
        var metadata = new Mock<IMetadataService>();
        var episodes = CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(
            row => row.AniDB_EpisodeID,
            new AniDB_Episode { AniDB_EpisodeID = 1, EpisodeID = EpisodeID, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode }
        );
        return new(new TmdbLinkingService(linking.Object, metadata.Object, episodes), linking, metadata);
    }

    #endregion

    #region Shows

    [Fact]
    public async Task LinkingAShowLeavesItsEpisodesToTheLinkingService()
    {
        var (service, linking, _) = Build();

        await service.AddShowLink(AnimeID, 5, additiveLink: false, MatchRating.TitleMatches);

        linking.Verify(l => l.AddSeriesLink(
            It.Is<MetadataSeriesLinkRequest>(request =>
                request.Source == MetadataSource.TMDB &&
                request.ProviderID == Show &&
                request.AnidbAnimeID == AnimeID &&
                !request.Additive &&
                request.MatchRating == MatchRating.TitleMatches),
            It.IsAny<CancellationToken>()
        ), Times.Once);

        // Linking a series matches its episodes by itself, so they are not
        // matched a second time.
        linking.Verify(l => l.MatchEpisodes(
            It.IsAny<int>(),
            It.IsAny<MetadataGuid>(),
            It.IsAny<MetadataGuid?>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<bool?>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public async Task RemovingAShowByHandTellsTmdbToLeaveTheAnimeAlone()
    {
        var (service, linking, _) = Build();

        await service.RemoveShowLink(AnimeID, 5, purge: true);
        await service.RemoveAllShowLinksForAnime(AnimeID, purge: true);

        linking.Verify(l => l.RemoveSeriesLink(
            It.Is<MetadataSeriesLinkRequest>(request => request.ProviderID == Show && request.Purge && request.DisableAutoLinking),
            It.IsAny<CancellationToken>()
        ), Times.Once);
        linking.Verify(
            l => l.RemoveLinksForAnime(MetadataSource.TMDB, AnimeID, MetadataEntityType.Series, true, true, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    #endregion

    #region Movies

    [Fact]
    public async Task AMovieLinkNamesTheEpisodesAnime()
    {
        var (service, linking, _) = Build();

        await service.AddMovieLinkForEpisode(EpisodeID, 7, additiveLink: true);

        linking.Verify(l => l.AddMovieLink(
            It.Is<MetadataEpisodeLinkRequest>(request =>
                request.ProviderID == Movie &&
                request.AnidbEpisodeID == EpisodeID &&
                request.AnidbAnimeID == AnimeID &&
                request.Additive),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task AMovieLinkForAnUnknownEpisodeIsNotMade()
    {
        var (service, linking, _) = Build();

        await service.AddMovieLinkForEpisode(999, 7);

        linking.Verify(l => l.AddMovieLink(It.IsAny<MetadataEpisodeLinkRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Episodes

    [Fact]
    public void AnEmptyEpisodeLinkReplacesTheRest()
    {
        var (service, linking, _) = Build();

        Assert.True(service.SetEpisodeLink(EpisodeID, 0, additiveLink: true));

        linking.Verify(l => l.SetEpisodeLink(MetadataSource.TMDB, EpisodeID, null, false, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void AnEpisodeTmdbDoesNotHoldIsNotLinked()
    {
        var (service, linking, _) = Build();

        Assert.False(service.SetEpisodeLink(EpisodeID, 42));

        linking.Verify(l => l.SetEpisodeLink(
            It.IsAny<MetadataSource>(),
            It.IsAny<int>(),
            It.IsAny<MetadataGuid?>(),
            It.IsAny<bool>(),
            It.IsAny<int?>(),
            It.IsAny<MetadataGuid?>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public void AStoredEpisodeIsLinkedAtItsPlace()
    {
        var (service, linking, metadata) = Build();
        var episodeID = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Episode, "42");
        metadata.Setup(m => m.GetEpisode(episodeID)).Returns(Mock.Of<IEpisode>());

        Assert.True(service.SetEpisodeLink(EpisodeID, 42, additiveLink: true, index: 2));

        linking.Verify(l => l.SetEpisodeLink(MetadataSource.TMDB, EpisodeID, episodeID, true, 2, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void MatchesComeBackAsTmdbLinks()
    {
        var (service, linking, _) = Build();
        var season = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Season, "9");
        IReadOnlyList<IMetadataEpisodeCrossReference> rows =
        [
            new CrossRef_AniDB_Metadata_Episode
            {
                Source = MetadataSource.TMDB,
                AnidbAnimeID = AnimeID,
                AnidbEpisodeID = EpisodeID,
                ProviderID = "42",
                ProviderParentID = "5",
                Ordering = 1,
                MatchRating = MatchRating.DateMatches,
            },
        ];
        linking.Setup(l => l.MatchEpisodes(AnimeID, Show, season, true, false, false, It.IsAny<CancellationToken>())).ReturnsAsync(rows);

        var links = service.MatchAnidbToTmdbEpisodes(AnimeID, 5, 9, useExisting: true, saveToDatabase: false, useExistingOtherShows: false);

        var link = Assert.Single(links);
        Assert.Equal("42", link.ProviderID?.ID);
        Assert.Equal("5", link.ProviderParentID?.ID);
        Assert.Equal(1, link.Ordering);
        Assert.Equal(MatchRating.DateMatches, link.MatchRating);
    }

    #endregion
}
