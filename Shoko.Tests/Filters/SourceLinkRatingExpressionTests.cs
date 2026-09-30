using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Filtering.Expressions.Info;
using Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Filters;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Filters;

/// <summary>
/// Covers the expressions asking how a series' series and movie-level links
/// to a source were made: the verified and automatic counts, and whether any
/// link is left to verify.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class SourceLinkRatingExpressionTests
{
    private static readonly DateTime s_date = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private const int GroupID = 1;

    private static readonly AnimeSeries s_first = new() { AnimeSeriesID = 1, AniDB_ID = 10, AnimeGroupID = GroupID };

    private static readonly AnimeSeries s_second = new() { AnimeSeriesID = 2, AniDB_ID = 20, AnimeGroupID = GroupID };

    #region Expressions read the counts they are named for

    [Fact]
    public void EveryExpressionAsksAboutItsOwnSource()
    {
        var filterable = new TestFilterable
        {
            AutomaticLinks = new Dictionary<MetadataSource, int> { [MetadataSource.TMDB] = 2 },
            UserVerifiedLinks = new Dictionary<MetadataSource, int> { [MetadataSource.TMDB] = 3, [TestSources.AniList] = 1 },
        };

        Assert.Equal(2d, new AutomaticSourceLinksSelector("tmdb").Evaluate(filterable, null, s_date));
        Assert.Equal(3d, new UserVerifiedSourceLinksSelector("TMDB").Evaluate(filterable, null, s_date));
        Assert.Equal(0d, new AutomaticSourceLinksSelector(TestSources.AniList.Value).Evaluate(filterable, null, s_date));
        Assert.True(new HasAutomaticSourceLinkExpression("tmdb").Evaluate(filterable, null, s_date));
        Assert.False(new HasAutomaticSourceLinkExpression(TestSources.AniList.Value).Evaluate(filterable, null, s_date));
    }

    [Fact]
    public void AParameterNamingNoSourceCountsNothing()
    {
        var filterable = new TestFilterable { AutomaticLinks = new Dictionary<MetadataSource, int> { [MetadataSource.TMDB] = 2 } };

        Assert.Equal(0d, new AutomaticSourceLinksSelector("no-such-source").Evaluate(filterable, null, s_date));
        Assert.False(new HasAutomaticSourceLinkExpression().Evaluate(filterable, null, s_date));
    }

    #endregion

    #region Counting off real series

    /// <summary>
    /// Two series in one group. The first has a verified TMDB show, an
    /// automatic TMDB film claiming the whole anime, an automatic TMDB film
    /// link on one episode, an automatic episode link that is not counted,
    /// and a verified link to nothing on another source. The second has one
    /// automatic TMDB show.
    /// </summary>
    private static RepoFactoryScope Scope()
        => new RepoFactoryScope()
            .With<AniDB_AnimeRepository, int, AniDB_Anime>(a => a.AniDB_AnimeID,
            [
                new() { AniDB_AnimeID = 1, AnimeID = 10, AnimeType = AnimeType.TV },
                new() { AniDB_AnimeID = 2, AnimeID = 20, AnimeType = AnimeType.TV },
            ])
            .With<AniDB_EpisodeRepository, int, AniDB_Episode>(e => e.AniDB_EpisodeID, [])
            .With<AnimeGroupRepository, int, AnimeGroup>(g => g.AnimeGroupID, [new() { AnimeGroupID = GroupID }])
            .With<AnimeSeriesRepository, int, AnimeSeries>(s => s.AnimeSeriesID, [s_first, s_second])
            .With<CrossRef_AniDB_Metadata_SeriesRepository, int, CrossRef_AniDB_Metadata_Series>(x => x.CrossRef_AniDB_Metadata_SeriesID,
            [
                new()
                {
                    CrossRef_AniDB_Metadata_SeriesID = 1, Source = MetadataSource.TMDB, AnidbAnimeID = 10, ProviderID = "300", MatchRating = MatchRating.UserVerified,
                },
                new()
                {
                    CrossRef_AniDB_Metadata_SeriesID = 2, Source = MetadataSource.TMDB, AnidbAnimeID = 10, ProviderID = "400", ProviderType = MetadataEntityType.Movie,
                    MatchRating = MatchRating.TitleMatches, Ordering = 1,
                },
                new()
                {
                    CrossRef_AniDB_Metadata_SeriesID = 3, Source = TestSources.AniList, AnidbAnimeID = 10, ProviderID = string.Empty,
                    MatchRating = MatchRating.UserVerified,
                },
                new()
                {
                    CrossRef_AniDB_Metadata_SeriesID = 4, Source = MetadataSource.TMDB, AnidbAnimeID = 20, ProviderID = "500", MatchRating = MatchRating.FirstAvailable,
                },
            ])
            .With<CrossRef_AniDB_Metadata_MovieRepository, int, CrossRef_AniDB_Metadata_Movie>(x => x.CrossRef_AniDB_Metadata_MovieID,
            [
                new()
                {
                    CrossRef_AniDB_Metadata_MovieID = 1, Source = MetadataSource.TMDB, AnidbAnimeID = 10, AnidbEpisodeID = 101, ProviderID = "400",
                    MatchRating = MatchRating.DateMatches,
                },
            ])
            .With<CrossRef_AniDB_Metadata_EpisodeRepository, int, CrossRef_AniDB_Metadata_Episode>(x => x.CrossRef_AniDB_Metadata_EpisodeID,
            [
                new()
                {
                    CrossRef_AniDB_Metadata_EpisodeID = 1, Source = MetadataSource.TMDB, AnidbAnimeID = 10, AnidbEpisodeID = 102, ProviderID = "301",
                    MatchRating = MatchRating.DateMatches,
                },
            ]);

    [Fact]
    public void ASeriesCountsItsSeriesAndFilmLinksButNotItsEpisodeLinks()
    {
        using var scope = Scope();
        IFilterableInfo filterable = new FilterableAnimeSeries(s_first, s_date);

        // Automatic: the film claiming the anime and the film on one episode.
        Assert.Equal(2d, new AutomaticSourceLinksSelector("tmdb").Evaluate(filterable, null, s_date));
        Assert.Equal(1d, new UserVerifiedSourceLinksSelector("tmdb").Evaluate(filterable, null, s_date));
        Assert.True(new HasAutomaticSourceLinkExpression("tmdb").Evaluate(filterable, null, s_date));
        // The verified link to nothing counts, and leaves nothing to verify.
        Assert.Equal(1d, new UserVerifiedSourceLinksSelector(TestSources.AniList.Value).Evaluate(filterable, null, s_date));
        Assert.False(new HasAutomaticSourceLinkExpression(TestSources.AniList.Value).Evaluate(filterable, null, s_date));
    }

    [Fact]
    public void AGroupAddsUpItsSeries()
    {
        using var scope = Scope();
        IFilterableInfo filterable = new FilterableAnimeGroup(RepoFactoryGroup(), s_date);

        Assert.Equal(3d, new AutomaticSourceLinksSelector("tmdb").Evaluate(filterable, null, s_date));
        Assert.Equal(1d, new UserVerifiedSourceLinksSelector("tmdb").Evaluate(filterable, null, s_date));
        Assert.True(new HasAutomaticSourceLinkExpression("tmdb").Evaluate(filterable, null, s_date));
    }

    [Fact]
    public void ASourceWithoutLinksHasNothingToVerify()
    {
        using var scope = Scope();
        IFilterableInfo filterable = new FilterableAnimeSeries(s_second, s_date);

        Assert.False(new HasAutomaticSourceLinkExpression(TestSources.AniList.Value).Evaluate(filterable, null, s_date));
    }

    private static AnimeGroup RepoFactoryGroup()
        => Server.Repositories.RepoFactory.AnimeGroup.GetByID(GroupID)!;

    #endregion
}
