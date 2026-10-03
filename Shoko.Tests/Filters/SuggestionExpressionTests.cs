using System;
using Shoko.Abstractions.Filtering.Expressions.Info;
using Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Filters;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata;
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

    private static TestFilterable Filterable(int anidb = 0, int tmdb = 0, int other = 0, int local = 0)
        => new()
        {
            AnidbSuggestions = anidb,
            TmdbSuggestions = tmdb,
            OtherSuggestions = other,
            TotalSuggestions = anidb + tmdb + other,
            LocalSuggestions = local,
        };

    [Fact]
    public void EverySelectorReadsItsOwnSource()
    {
        var filterable = Filterable(anidb: 3, tmdb: 5, other: 7, local: 2);

        Assert.Equal(3d, new AnidbSuggestionCountSelector().Evaluate(filterable, null, s_date));
        Assert.Equal(5d, new TmdbSuggestionCountSelector().Evaluate(filterable, null, s_date));
        Assert.Equal(7d, new SourceSuggestionCountSelector(TestSources.Plugin.Value).Evaluate(filterable, null, s_date));
        Assert.Equal(15d, new TotalSuggestionCountSelector().Evaluate(filterable, null, s_date));
        Assert.Equal(2d, new LocalSuggestionCountSelector().Evaluate(filterable, null, s_date));
    }

    [Fact]
    public void EveryBooleanIsFalseWithoutSuggestions()
    {
        var filterable = Filterable();

        Assert.False(new HasAnidbSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.False(new HasTmdbSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.False(new HasSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.False(new HasLocalSuggestionExpression().Evaluate(filterable, null, s_date));
    }

    [Theory]
    [InlineData(1, 0, true, false)]
    [InlineData(0, 1, false, true)]
    public void EveryBooleanOnlyLooksAtItsOwnSource(int anidb, int tmdb, bool hasAnidb, bool hasTmdb)
    {
        var filterable = Filterable(anidb, tmdb);

        Assert.Equal(hasAnidb, new HasAnidbSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.Equal(hasTmdb, new HasTmdbSuggestionExpression().Evaluate(filterable, null, s_date));
        // Whichever one it was, the total saw it.
        Assert.True(new HasSuggestionExpression().Evaluate(filterable, null, s_date));
    }

    [Fact]
    public void TheLocalBooleanIsFalseWhenEverySuggestionPointsOutsideTheCollection()
    {
        // The common case: a series with plenty of suggestions, none of which are held.
        var filterable = Filterable(anidb: 10, tmdb: 10, other: 10);

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

    // Anime 10 suggests six entries across AniDB and TMDB, two resolving back to anime 11, the
    // collection's only other series; anime 11 suggests nothing, though two suggestions point at it.

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
            // Every source's series links live in one table.
            .With<CrossRef_AniDB_Metadata_SeriesRepository, int, CrossRef_AniDB_Metadata_Series>(x => x.CrossRef_AniDB_Metadata_SeriesID,
            [
                new() { CrossRef_AniDB_Metadata_SeriesID = 1, Source = MetadataSource.TMDB, AnidbAnimeID = SuggestingAnidbID, ProviderID = "200" },
                new() { CrossRef_AniDB_Metadata_SeriesID = 2, Source = MetadataSource.TMDB, AnidbAnimeID = HeldAnidbID, ProviderID = "201" },
            ])
            .With<CrossRef_AniDB_Metadata_MovieRepository, int, CrossRef_AniDB_Metadata_Movie>(x => x.CrossRef_AniDB_Metadata_MovieID,
            [
                new() { CrossRef_AniDB_Metadata_MovieID = 1, Source = MetadataSource.TMDB, AnidbAnimeID = SuggestingAnidbID, AnidbEpisodeID = 1000, ProviderID = "300" },
            ])
            .With<CrossRef_AniDB_Metadata_EpisodeRepository, int, CrossRef_AniDB_Metadata_Episode>(x => x.CrossRef_AniDB_Metadata_EpisodeID, [])
            .With<Metadata_SuggestionRepository, int, Metadata_Suggestion>(s => s.Metadata_SuggestionID,
            [
                TmdbSuggestion(1, MetadataEntityType.Series, "200", "201", SuggestionKind.Recommended),
                TmdbSuggestion(2, MetadataEntityType.Series, "200", "202", SuggestionKind.Similar),
                TmdbSuggestion(3, MetadataEntityType.Movie, "300", "301", SuggestionKind.Recommended),
            ]);

    private static Metadata_Suggestion TmdbSuggestion(int rowID, MetadataEntityType entityType, string baseID, string suggestedID, SuggestionKind kind)
        => new()
        {
            Metadata_SuggestionID = rowID,
            Source = MetadataSource.TMDB,
            BaseType = entityType,
            BaseID = baseID,
            SuggestedType = entityType,
            SuggestedID = suggestedID,
            Kind = kind,
        };

    [Fact]
    public void ASeriesWithSuggestionsFromEverySourceCountsEachOne()
    {
        using var scope = Scope();
        var filterable = new FilterableAnimeSeries(s_suggestingSeries, s_date);

        Assert.Equal(3, filterable.AnidbSuggestions);
        // Two from the linked show plus one from the linked movie.
        Assert.Equal(3, filterable.GetSuggestions(MetadataSource.TMDB));
        Assert.Equal(6, filterable.TotalSuggestions);
    }

    [Fact]
    public void OnlyTheSuggestionsResolvingToAHeldSeriesAreLocal()
    {
        using var scope = Scope();
        var filterable = new FilterableAnimeSeries(s_suggestingSeries, s_date);

        // One per source: AniDB anime 11 and TMDB show 201 both trace back to the one other series
        // in the collection. The other four point outside it.
        Assert.Equal(2, filterable.LocalSuggestions);
        Assert.True(new HasLocalSuggestionExpression().Evaluate(filterable, null, s_date));
    }

    [Fact]
    public void ASeriesThatSuggestsNothingCountsZeroEvenWhenItIsSuggested()
    {
        using var scope = Scope();
        var filterable = new FilterableAnimeSeries(s_heldSeries, s_date);

        // Two suggestions point at this series, but the expressions measure what a series suggests.
        Assert.False(new HasSuggestionExpression().Evaluate(filterable, null, s_date));
    }

    [Fact]
    public void TheBooleansAgreeWithTheCountsOnARealSeries()
    {
        using var scope = Scope();
        var filterable = new FilterableAnimeSeries(s_suggestingSeries, s_date);

        Assert.True(new HasAnidbSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.True(new HasTmdbSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.True(new HasSuggestionExpression().Evaluate(filterable, null, s_date));
        Assert.Equal(6d, new TotalSuggestionCountSelector().Evaluate(filterable, null, s_date));
    }

    #endregion
}
