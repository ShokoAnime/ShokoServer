using System;
using Shoko.Abstractions.Filtering.Expressions.Info;
using Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Filters;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Anilist;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Filters;

/// <summary>
/// Covers the suggestion expressions: the per-source counts, the total, the count of suggestions
/// landing inside the collection, and the booleans built on top of them.
/// </summary>
/// <remarks>
/// Two halves. The first reads the expressions off a populated double, so a selector wired to the
/// wrong property fails. The second builds a real <see cref="FilterableAnimeSeries"/> over
/// cache-backed repositories, which is where the counting itself lives and where the difference
/// between what a series suggests and what suggests it actually shows up.
/// </remarks>
[Collection(nameof(RepoFactoryCollection))]
public class SuggestionExpressionTests
{
    private static readonly DateTime s_date = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    #region Expressions read the property they are named for

    private static TestFilterable Filterable(int anidb = 0, int tmdb = 0, int anilist = 0, int local = 0)
        => new()
        {
            AnidbSuggestions = anidb,
            TmdbSuggestions = tmdb,
            AnilistSuggestions = anilist,
            TotalSuggestions = anidb + tmdb + anilist,
            LocalSuggestions = local,
        };

    [Fact]
    public void EverySelectorReadsItsOwnSource()
    {
        var filterable = Filterable(anidb: 3, tmdb: 5, anilist: 7, local: 2);

        Assert.Equal(3d, new AnidbSuggestionCountSelector().Evaluate(filterable, null, s_date));
        Assert.Equal(5d, new TmdbSuggestionCountSelector().Evaluate(filterable, null, s_date));
        Assert.Equal(7d, new AnilistSuggestionCountSelector().Evaluate(filterable, null, s_date));
        Assert.Equal(15d, new TotalSuggestionCountSelector().Evaluate(filterable, null, s_date));
        Assert.Equal(2d, new LocalSuggestionCountSelector().Evaluate(filterable, null, s_date));
    }

    [Fact]
    public void EveryBooleanIsFalseWithoutSuggestions()
    {
        var filterable = Filterable();

        Assert.False(new HasAnidbSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.False(new HasTmdbSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.False(new HasAnilistSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.False(new HasSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.False(new HasLocalSuggestionExpression().Evaluate(filterable, null, s_date));
    }

    [Theory]
    [InlineData(1, 0, 0, true, false, false)]
    [InlineData(0, 1, 0, false, true, false)]
    [InlineData(0, 0, 1, false, false, true)]
    public void EveryBooleanOnlyLooksAtItsOwnSource(int anidb, int tmdb, int anilist, bool hasAnidb, bool hasTmdb, bool hasAnilist)
    {
        var filterable = Filterable(anidb, tmdb, anilist);

        Assert.Equal(hasAnidb, new HasAnidbSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.Equal(hasTmdb, new HasTmdbSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.Equal(hasAnilist, new HasAnilistSuggestionExpression().Evaluate(filterable, null, s_date));
        // Whichever one it was, the total saw it.
        Assert.True(new HasSuggestionExpression().Evaluate(filterable, null, s_date));
    }

    [Fact]
    public void TheLocalBooleanIsFalseWhenEverySuggestionPointsOutsideTheCollection()
    {
        // The common case: a series with plenty of suggestions, none of which are held.
        var filterable = Filterable(anidb: 10, tmdb: 10, anilist: 10);

        Assert.True(new HasSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.False(new HasLocalSuggestionExpression().Evaluate(filterable, null, s_date));
    }

    [Fact]
    public void NoneOfTheExpressionsNeedAUser()
    {
        Assert.False(new TotalSuggestionCountSelector().UserDependent);
        Assert.False(new HasSuggestionExpression().UserDependent);
        Assert.False(new HasLocalSuggestionExpression().UserDependent);
        Assert.False(new TotalSuggestionCountSelector().TimeDependent);
        Assert.False(new HasSuggestionExpression().TimeDependent);
    }

    #endregion

    #region Counting off a real series

    private const int SuggestingAnidbID = 10;

    private const int HeldAnidbID = 11;

    private const int SuggestingSeriesID = 1;

    private const int HeldSeriesID = 2;

    private static readonly AnimeSeries s_suggestingSeries = new() { AnimeSeriesID = SuggestingSeriesID, AniDB_ID = SuggestingAnidbID };

    private static readonly AnimeSeries s_heldSeries = new() { AnimeSeriesID = HeldSeriesID, AniDB_ID = HeldAnidbID };

    /// <summary>
    /// Anime 10 suggests eight entries across the three sources. Three of them resolve back to
    /// anime 11, which is the only other series in the collection. Anime 11 suggests nothing of
    /// its own, so it is the "no suggestions" case even though three suggestions point at it.
    /// </summary>
    private static RepoFactoryScope Scope()
        => new RepoFactoryScope()
            .With<AniDB_AnimeRepository, int, AniDB_Anime>(a => a.AniDB_AnimeID)
            .With<AnimeSeriesRepository, int, AnimeSeries>(s => s.AnimeSeriesID, [s_suggestingSeries, s_heldSeries])
            .With<AniDB_Anime_SimilarRepository, int, AniDB_Anime_Similar>(a => a.AniDB_Anime_SimilarID,
            [
                new() { AniDB_Anime_SimilarID = 1, AnimeID = SuggestingAnidbID, SimilarAnimeID = HeldAnidbID, Approval = 9, Total = 10 },
                new() { AniDB_Anime_SimilarID = 2, AnimeID = SuggestingAnidbID, SimilarAnimeID = 12, Approval = 5, Total = 10, Ordering = 1 },
                new() { AniDB_Anime_SimilarID = 3, AnimeID = SuggestingAnidbID, SimilarAnimeID = 13, Approval = 1, Total = 10, Ordering = 2 },
            ])
            .With<CrossRef_AniDB_TMDB_ShowRepository, int, CrossRef_AniDB_TMDB_Show>(x => x.CrossRef_AniDB_TMDB_ShowID,
            [
                new() { CrossRef_AniDB_TMDB_ShowID = 1, AnidbAnimeID = SuggestingAnidbID, TmdbShowID = 200 },
                new() { CrossRef_AniDB_TMDB_ShowID = 2, AnidbAnimeID = HeldAnidbID, TmdbShowID = 201 },
            ])
            .With<CrossRef_AniDB_TMDB_MovieRepository, int, CrossRef_AniDB_TMDB_Movie>(x => x.CrossRef_AniDB_TMDB_MovieID,
            [
                new() { CrossRef_AniDB_TMDB_MovieID = 1, AnidbAnimeID = SuggestingAnidbID, AnidbEpisodeID = 1000, TmdbMovieID = 300 },
            ])
            .With<TMDB_SuggestionRepository, int, TMDB_Suggestion>(s => s.TMDB_SuggestionID,
            [
                new() { TMDB_SuggestionID = 1, TmdbEntityType = DataEntityType.Show, TmdbEntityID = 200, SuggestedTmdbEntityID = 201, Kind = SuggestionKind.Recommended },
                new() { TMDB_SuggestionID = 2, TmdbEntityType = DataEntityType.Show, TmdbEntityID = 200, SuggestedTmdbEntityID = 202, Kind = SuggestionKind.Similar },
                new() { TMDB_SuggestionID = 3, TmdbEntityType = DataEntityType.Movie, TmdbEntityID = 300, SuggestedTmdbEntityID = 301, Kind = SuggestionKind.Recommended },
            ])
            .With<CrossRef_AniDB_Anilist_AnimeRepository, int, CrossRef_AniDB_Anilist_Anime>(x => x.CrossRef_AniDB_Anilist_AnimeID,
            [
                new() { CrossRef_AniDB_Anilist_AnimeID = 1, AnidbAnimeID = SuggestingAnidbID, AnilistAnimeID = 400 },
                new() { CrossRef_AniDB_Anilist_AnimeID = 2, AnidbAnimeID = HeldAnidbID, AnilistAnimeID = 401 },
            ])
            .With<Anilist_Anime_SuggestionRepository, int, Anilist_Anime_Suggestion>(s => s.Anilist_Anime_SuggestionID,
            [
                new() { Anilist_Anime_SuggestionID = 1, AnilistAnimeID = 400, SuggestedAnilistAnimeID = 401, Rating = 20 },
                new() { Anilist_Anime_SuggestionID = 2, AnilistAnimeID = 400, SuggestedAnilistAnimeID = 402, Rating = 10, Ordering = 1 },
            ]);

    [Fact]
    public void ASeriesWithSuggestionsFromEverySourceCountsEachOne()
    {
        using var scope = Scope();
        var filterable = new FilterableAnimeSeries(s_suggestingSeries, s_date);

        Assert.Equal(3, filterable.AnidbSuggestions);
        // Two from the linked show plus one from the linked movie.
        Assert.Equal(3, filterable.TmdbSuggestions);
        Assert.Equal(2, filterable.AnilistSuggestions);
        Assert.Equal(8, filterable.TotalSuggestions);
    }

    [Fact]
    public void OnlyTheSuggestionsResolvingToAHeldSeriesAreLocal()
    {
        using var scope = Scope();
        var filterable = new FilterableAnimeSeries(s_suggestingSeries, s_date);

        // One per source: AniDB anime 11, TMDB show 201 and AniList anime 401 all trace back to the
        // one other series in the collection. The other five point outside it.
        Assert.Equal(3, filterable.LocalSuggestions);
        Assert.True(new HasLocalSuggestionExpression().Evaluate(filterable, null, s_date));
    }

    [Fact]
    public void ASeriesThatSuggestsNothingCountsZeroEvenWhenItIsSuggested()
    {
        using var scope = Scope();
        var filterable = new FilterableAnimeSeries(s_heldSeries, s_date);

        // Three suggestions point at this series, and none of them count here: the expressions
        // measure what a series suggests, not what suggests it.
        Assert.Equal(0, filterable.AnidbSuggestions);
        Assert.Equal(0, filterable.TmdbSuggestions);
        Assert.Equal(0, filterable.AnilistSuggestions);
        Assert.Equal(0, filterable.TotalSuggestions);
        Assert.Equal(0, filterable.LocalSuggestions);

        Assert.False(new HasAnidbSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.False(new HasTmdbSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.False(new HasAnilistSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.False(new HasSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.False(new HasLocalSuggestionExpression().Evaluate(filterable, null, s_date));
    }

    [Fact]
    public void TheBooleansAgreeWithTheCountsOnARealSeries()
    {
        using var scope = Scope();
        var filterable = new FilterableAnimeSeries(s_suggestingSeries, s_date);

        Assert.True(new HasAnidbSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.True(new HasTmdbSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.True(new HasAnilistSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.True(new HasSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.Equal(8d, new TotalSuggestionCountSelector().Evaluate(filterable, null, s_date));
    }

    #endregion
}
