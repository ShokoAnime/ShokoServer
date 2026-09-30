using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Filtering.Expressions.Info;
using Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;
using Shoko.Abstractions.Filtering.Expressions.Selectors.StringSetSelectors;
using Shoko.Abstractions.Filtering.Sorting.Selectors;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Filters;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Filters;

/// <summary>
/// Covers the TMDB filter expressions, which are thin wrappers over the generic source expressions
/// with the tmdb source. Their names and saved JSON must stay as they were, so saved filters and the
/// WebUI keep working, and they must give what the generic expressions give.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class TmdbFilterExpressionWrapperTests
{
    private static readonly DateTime s_date = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static readonly string s_tmdb = MetadataSource.TMDB.Value;

    #region The wrappers give what the generic expressions give

    private static IReadOnlySet<string> Names(params string[] names)
        => new HashSet<string>(names, StringComparer.InvariantCultureIgnoreCase);

    /// <summary>
    /// A few doubles, each with TMDB answers the others lack, so a wrapper reading the wrong member
    /// disagrees with its generic twin on at least one of them.
    /// </summary>
    private static IEnumerable<TestFilterable> Filterables()
    {
        yield return new() { AnimeTypes = new HashSet<AnimeType> { AnimeType.TV } };
        yield return new()
        {
            AnimeTypes = new HashSet<AnimeType> { AnimeType.TV },
            LinkedSources = new HashSet<MetadataSource> { MetadataSource.TMDB },
            AutomaticEpisodeLinks = new Dictionary<MetadataSource, int> { [MetadataSource.TMDB] = 3 },
            UserVerifiedEpisodeLinks = new Dictionary<MetadataSource, int> { [MetadataSource.TMDB] = 2 },
            MissingEpisodeLinks = new Dictionary<MetadataSource, int> { [MetadataSource.TMDB] = 1 },
            Genres = new Dictionary<MetadataSource, IReadOnlySet<string>> { [MetadataSource.TMDB] = Names("Drama", "Comedy") },
            SourceTags = new Dictionary<MetadataSource, IReadOnlySet<string>> { [MetadataSource.TMDB] = Names("school", "robot") },
            TmdbSuggestions = 4,
        };
        yield return new()
        {
            AnimeTypes = new HashSet<AnimeType> { AnimeType.Movie },
            AutoLinkingDisabledSources = new HashSet<MetadataSource> { MetadataSource.TMDB },
        };
        yield return new()
        {
            AnimeTypes = new HashSet<AnimeType> { AnimeType.OVA },
            UnlinkedSources = new HashSet<MetadataSource> { MetadataSource.TMDB },
        };
        yield return new() { AnimeTypes = new HashSet<AnimeType> { AnimeType.MusicVideo } };
    }

    public static TheoryData<string> BooleanPairs()
        => [.. s_booleanPairs.Keys];

    private static readonly Dictionary<string, (Func<FilterExpression<bool>> Wrapper, Func<FilterExpression<bool>> Generic)> s_booleanPairs = new()
    {
        [nameof(HasTmdbLinkExpression)] = (() => new HasTmdbLinkExpression(), () => new HasSourceLinkExpression(s_tmdb)),
        [nameof(MissingTmdbLinkExpression)] = (() => new MissingTmdbLinkExpression(), () => new MissingSourceLinkExpression(s_tmdb)),
        [nameof(HasTmdbAutoLinkingDisabledExpression)] = (() => new HasTmdbAutoLinkingDisabledExpression(), () => new HasSourceAutoLinkingDisabledExpression(s_tmdb)),
        [nameof(HasTmdbSuggestionExpression)] = (() => new HasTmdbSuggestionExpression(), () => new HasSourceSuggestionExpression(s_tmdb)),
        [nameof(HasTmdbGenreExpression)] = (() => new HasTmdbGenreExpression("drama"), () => new HasSourceGenreExpression(s_tmdb, "drama")),
        [nameof(HasTmdbKeywordExpression)] = (() => new HasTmdbKeywordExpression("Robot"), () => new HasSourceTagExpression(s_tmdb, "Robot")),
    };

    [Theory]
    [MemberData(nameof(BooleanPairs))]
    public void EveryTmdbConditionAgreesWithItsGenericTwin(string name)
    {
        var (wrapper, generic) = s_booleanPairs[name];
        foreach (var filterable in Filterables())
            Assert.Equal(generic().Evaluate(filterable, null, s_date), wrapper().Evaluate(filterable, null, s_date));
    }

    public static TheoryData<string> NumberPairs()
        => [.. s_numberPairs.Keys];

    private static readonly Dictionary<string, (Func<FilterExpression<double>> Wrapper, Func<FilterExpression<double>> Generic)> s_numberPairs = new()
    {
        [nameof(AutomaticTmdbEpisodeLinksSelector)] = (() => new AutomaticTmdbEpisodeLinksSelector(), () => new AutomaticSourceEpisodeLinksSelector(s_tmdb)),
        [nameof(UserVerifiedTmdbEpisodeLinksSelector)] = (() => new UserVerifiedTmdbEpisodeLinksSelector(), () => new UserVerifiedSourceEpisodeLinksSelector(s_tmdb)),
        [nameof(MissingTmdbEpisodeLinksSelector)] = (() => new MissingTmdbEpisodeLinksSelector(), () => new MissingSourceEpisodeLinksSelector(s_tmdb)),
        [nameof(TmdbSuggestionCountSelector)] = (() => new TmdbSuggestionCountSelector(), () => new SourceSuggestionCountSelector(s_tmdb)),
    };

    [Theory]
    [MemberData(nameof(NumberPairs))]
    public void EveryTmdbCountAgreesWithItsGenericTwin(string name)
    {
        var (wrapper, generic) = s_numberPairs[name];
        foreach (var filterable in Filterables())
            Assert.Equal(generic().Evaluate(filterable, null, s_date), wrapper().Evaluate(filterable, null, s_date));
    }

    [Fact]
    public void TheTmdbSetsAgreeWithTheirGenericTwins()
    {
        foreach (var filterable in Filterables())
        {
            Assert.Equal(new SourceGenresSelector(s_tmdb).Evaluate(filterable, null, s_date), new TmdbGenresSelector().Evaluate(filterable, null, s_date));
            Assert.Equal(new SourceTagsSelector(s_tmdb).Evaluate(filterable, null, s_date), new TmdbKeywordsSelector().Evaluate(filterable, null, s_date));
            Assert.Equal(
                new SourceSuggestionCountSortingSelector(s_tmdb).Evaluate(filterable, null, s_date),
                new TmdbSuggestionCountSortingSelector().Evaluate(filterable, null, s_date)
            );
        }
    }

    [Fact]
    public void AnySourceLinkedToNothingOnPurposeIsNotMissingALink()
    {
        var plugin = TestSources.Plugin;
        var missing = new MissingSourceLinkExpression(plugin.Value);
        var types = new HashSet<AnimeType> { AnimeType.TV };

        Assert.True(missing.Evaluate(new TestFilterable { AnimeTypes = types }, null, s_date));
        Assert.False(missing.Evaluate(new TestFilterable { AnimeTypes = types, UnlinkedSources = new HashSet<MetadataSource> { plugin } }, null, s_date));
        Assert.False(missing.Evaluate(new TestFilterable { AnimeTypes = types, LinkedSources = new HashSet<MetadataSource> { plugin } }, null, s_date));
        Assert.False(missing.Evaluate(new TestFilterable { AnimeTypes = types, AutoLinkingDisabledSources = new HashSet<MetadataSource> { plugin } }, null, s_date));
        Assert.False(missing.Evaluate(new TestFilterable { AnimeTypes = new HashSet<AnimeType> { AnimeType.Other } }, null, s_date));
    }

    [Fact]
    public void AnySourceLinkedToNothingOnPurposeHasALink()
    {
        var plugin = TestSources.Plugin;
        var has = new HasSourceLinkExpression(plugin.Value);

        Assert.False(has.Evaluate(new TestFilterable(), null, s_date));
        Assert.True(has.Evaluate(new TestFilterable { UnlinkedSources = new HashSet<MetadataSource> { plugin } }, null, s_date));
        Assert.True(has.Evaluate(new TestFilterable { LinkedSources = new HashSet<MetadataSource> { plugin } }, null, s_date));
        Assert.False(has.Evaluate(new TestFilterable { UnlinkedSources = new HashSet<MetadataSource> { MetadataSource.TMDB } }, null, s_date));
    }

    [Fact]
    public void AParameterChangedAfterEvaluationIsAskedAboutAfresh()
    {
        var filterable = new TestFilterable
        {
            Genres = new Dictionary<MetadataSource, IReadOnlySet<string>> { [MetadataSource.TMDB] = Names("Drama") },
        };
        var expression = new HasTmdbGenreExpression("Drama");

        Assert.True(expression.Evaluate(filterable, null, s_date));
        expression.Parameter = "Horror";
        Assert.False(expression.Evaluate(filterable, null, s_date));
        expression.Parameter = null;
        Assert.False(expression.Evaluate(filterable, null, s_date));
    }

    #endregion

    #region Saved filters read the same

    private static readonly FilterExpressionConverter s_converter = new();

    [Theory]
    [InlineData(typeof(HasTmdbLinkExpression), "HasTmdbLink", FilterExpressionParameterType.Expression, null)]
    [InlineData(typeof(MissingTmdbLinkExpression), "MissingTmdbLink", FilterExpressionParameterType.Expression, null)]
    [InlineData(typeof(HasTmdbAutoLinkingDisabledExpression), "HasTmdbAutoLinkingDisabled", FilterExpressionParameterType.Expression, null)]
    [InlineData(typeof(HasTmdbSuggestionExpression), "HasTmdbSuggestion", FilterExpressionParameterType.Expression, null)]
    [InlineData(typeof(HasTmdbShowGenreExpression), "HasTmdbShowGenre", FilterExpressionParameterType.Expression, FilterExpressionParameterType.String)]
    [InlineData(typeof(HasTmdbMovieKeywordExpression), "HasTmdbMovieKeyword", FilterExpressionParameterType.Expression, FilterExpressionParameterType.String)]
    [InlineData(typeof(AutomaticTmdbEpisodeLinksSelector), "AutomaticTmdbEpisodeLinks", FilterExpressionParameterType.NumberSelector, null)]
    [InlineData(typeof(TmdbSuggestionCountSelector), "TmdbSuggestionCount", FilterExpressionParameterType.NumberSelector, null)]
    [InlineData(typeof(TmdbShowKeywordsSelector), "TmdbShowKeywords", FilterExpressionParameterType.StringSetSelector, null)]
    public void TheTmdbExpressionsAreListedAsBefore(Type type, string expression, FilterExpressionParameterType kind, FilterExpressionParameterType? parameter)
    {
        // The series below is linked to a show with the genre Drama and a movie with the keyword space.
        using var scope = Scope();

        var help = ExpressionDiscovery.GetExpressionHelp(type)!;

        Assert.Equal(expression, help.Expression);
        Assert.Equal(kind, help.Type);
        Assert.Equal(parameter, help.Parameter);
        if (type == typeof(HasTmdbShowGenreExpression))
            Assert.Equal(["Drama"], help.PossibleParameters!);
        if (type == typeof(HasTmdbMovieKeywordExpression))
            Assert.Equal(["space"], help.PossibleParameters!);
    }

    public static TheoryData<string, string> SavedForms()
        => new()
        {
            { nameof(HasTmdbLinkExpression), """{"$type":"HasTmdbLinkExpression"}""" },
            { nameof(MissingTmdbLinkExpression), """{"$type":"MissingTmdbLinkExpression"}""" },
            { nameof(HasTmdbAutoLinkingDisabledExpression), """{"$type":"HasTmdbAutoLinkingDisabledExpression"}""" },
            { nameof(HasTmdbSuggestionExpression), """{"$type":"HasTmdbSuggestionExpression"}""" },
            { nameof(HasTmdbGenreExpression), """{"$type":"HasTmdbGenreExpression","Parameter":"Drama"}""" },
            { nameof(HasTmdbKeywordExpression), """{"$type":"HasTmdbKeywordExpression","Parameter":"Drama"}""" },
            { nameof(HasTmdbShowGenreExpression), """{"$type":"HasTmdbShowGenreExpression","Parameter":"Drama"}""" },
            { nameof(HasTmdbMovieGenreExpression), """{"$type":"HasTmdbMovieGenreExpression","Parameter":"Drama"}""" },
            { nameof(HasTmdbShowKeywordExpression), """{"$type":"HasTmdbShowKeywordExpression","Parameter":"Drama"}""" },
            { nameof(HasTmdbMovieKeywordExpression), """{"$type":"HasTmdbMovieKeywordExpression","Parameter":"Drama"}""" },
            { nameof(AutomaticTmdbEpisodeLinksSelector), """{"$type":"AutomaticTmdbEpisodeLinksSelector"}""" },
            { nameof(UserVerifiedTmdbEpisodeLinksSelector), """{"$type":"UserVerifiedTmdbEpisodeLinksSelector"}""" },
            { nameof(MissingTmdbEpisodeLinksSelector), """{"$type":"MissingTmdbEpisodeLinksSelector"}""" },
            { nameof(TmdbSuggestionCountSelector), """{"$type":"TmdbSuggestionCountSelector"}""" },
            { nameof(TmdbGenresSelector), """{"$type":"TmdbGenresSelector"}""" },
            { nameof(TmdbKeywordsSelector), """{"$type":"TmdbKeywordsSelector"}""" },
            { nameof(TmdbShowGenresSelector), """{"$type":"TmdbShowGenresSelector"}""" },
            { nameof(TmdbMovieGenresSelector), """{"$type":"TmdbMovieGenresSelector"}""" },
            { nameof(TmdbShowKeywordsSelector), """{"$type":"TmdbShowKeywordsSelector"}""" },
            { nameof(TmdbMovieKeywordsSelector), """{"$type":"TmdbMovieKeywordsSelector"}""" },
            { nameof(TmdbSuggestionCountSortingSelector), """{"$type":"TmdbSuggestionCountSortingSelector","Descending":false,"Next":null}""" },
        };

    [Theory]
    [MemberData(nameof(SavedForms))]
    public void EveryTmdbExpressionIsSavedAsItAlwaysWas(string name, string saved)
    {
        var type = Array.Find(typeof(FilterExpression).Assembly.GetTypes(), t => t.Name == name)!;
        var expression = (FilterExpression)Activator.CreateInstance(type)!;
        if (expression is IWithStringParameter withParameter)
            withParameter.Parameter = "Drama";

        Assert.Equal(saved, s_converter.ConvertTo(null, null, expression, typeof(string)));

        var read = s_converter.ConvertFrom(null, null, saved);
        Assert.IsType(type, read);
        Assert.Equal(saved, s_converter.ConvertTo(null, null, read, typeof(string)));
    }

    #endregion

    #region Counting off a real series

    private const int AnidbID = 20;

    private const int SeriesID = 5;

    private static readonly AnimeSeries s_series = new() { AnimeSeriesID = SeriesID, AniDB_ID = AnidbID };

    private static MetadataCrossReferenceStore ReadOnlyStore()
        => new(
            RepoFactory.CrossRef_AniDB_Metadata_Series,
            RepoFactory.CrossRef_AniDB_Metadata_Movie,
            RepoFactory.CrossRef_AniDB_Metadata_Episode,
            CachedRepo.Build<Metadata_EpisodeRepository, int, Metadata_Episode>(row => row.Metadata_EpisodeID)
        );

    /// <summary>
    /// A series linked to one TMDB show and one TMDB movie, each with its own genres and keywords,
    /// whose three episodes are linked, linked to nothing by a person, and linked to nothing by the
    /// matcher.
    /// </summary>
    /// <param name="seriesLinks">The series-level TMDB links.</param>
    /// <param name="withFilm">Whether the third episode stands for the TMDB movie.</param>
    private static RepoFactoryScope Scope(IEnumerable<CrossRef_AniDB_Metadata_Series>? seriesLinks = null, bool withFilm = true)
        => new RepoFactoryScope()
            .With<AniDB_AnimeRepository, int, AniDB_Anime>(a => a.AniDB_AnimeID, [new() { AniDB_AnimeID = 1, AnimeID = AnidbID, AnimeType = AnimeType.TV }])
            .With<AnimeSeriesRepository, int, AnimeSeries>(s => s.AnimeSeriesID, [s_series])
            .With<AniDB_EpisodeRepository, int, AniDB_Episode>(e => e.AniDB_EpisodeID,
            [
                new() { AniDB_EpisodeID = 1, EpisodeID = 201, AnimeID = AnidbID, EpisodeNumber = 1 },
                new() { AniDB_EpisodeID = 2, EpisodeID = 202, AnimeID = AnidbID, EpisodeNumber = 2 },
                new() { AniDB_EpisodeID = 3, EpisodeID = 203, AnimeID = AnidbID, EpisodeNumber = 3 },
            ])
            .With<AnimeEpisodeRepository, int, AnimeEpisode>(e => e.AnimeEpisodeID,
            [
                new() { AnimeEpisodeID = 1, AnimeSeriesID = SeriesID, AniDB_EpisodeID = 201 },
                new() { AnimeEpisodeID = 2, AnimeSeriesID = SeriesID, AniDB_EpisodeID = 202 },
                new() { AnimeEpisodeID = 3, AnimeSeriesID = SeriesID, AniDB_EpisodeID = 203 },
            ])
            .With<CrossRef_AniDB_Metadata_SeriesRepository, int, CrossRef_AniDB_Metadata_Series>(x => x.CrossRef_AniDB_Metadata_SeriesID,
                seriesLinks ?? [new() { CrossRef_AniDB_Metadata_SeriesID = 1, Source = MetadataSource.TMDB, AnidbAnimeID = AnidbID, ProviderID = "300" }])
            .With<CrossRef_AniDB_Metadata_MovieRepository, int, CrossRef_AniDB_Metadata_Movie>(x => x.CrossRef_AniDB_Metadata_MovieID, withFilm
                ? [new() { CrossRef_AniDB_Metadata_MovieID = 1, Source = MetadataSource.TMDB, AnidbAnimeID = AnidbID, AnidbEpisodeID = 203, ProviderID = "400" }]
                : [])
            .With<CrossRef_AniDB_Metadata_EpisodeRepository, int, CrossRef_AniDB_Metadata_Episode>(x => x.CrossRef_AniDB_Metadata_EpisodeID,
            [
                new() { CrossRef_AniDB_Metadata_EpisodeID = 1, Source = MetadataSource.TMDB, AnidbAnimeID = AnidbID, AnidbEpisodeID = 201, ProviderID = "301", ProviderParentID = "300" },
                new()
                {
                    CrossRef_AniDB_Metadata_EpisodeID = 2, Source = MetadataSource.TMDB, AnidbAnimeID = AnidbID, AnidbEpisodeID = 202, ProviderID = string.Empty,
                    MatchRating = MatchRating.UserVerified,
                },
                new() { CrossRef_AniDB_Metadata_EpisodeID = 3, Source = MetadataSource.TMDB, AnidbAnimeID = AnidbID, AnidbEpisodeID = 203, ProviderID = string.Empty },
            ])
            .Set(new CrossRef_AniDB_TMDB_ShowRepository(RepoFactory.CrossRef_AniDB_Metadata_Series, ReadOnlyStore()))
            .Set(new CrossRef_AniDB_TMDB_MovieRepository(RepoFactory.CrossRef_AniDB_Metadata_Movie, ReadOnlyStore()))
            .With<TMDB_ShowRepository, int, TMDB_Show>(s => s.Id, [new() { TmdbShowID = 300, Genres = ["Drama"], Keywords = ["school"] }])
            .With<TMDB_MovieRepository, int, TMDB_Movie>(m => m.Id, [new() { TmdbMovieID = 400, Genres = ["Action"], Keywords = ["space"] }]);

    [Fact]
    public void TheShowAndMovieGenresAndKeywordsComeFromTheirOwnEntries()
    {
        using var scope = Scope();
        IFilterableInfo filterable = new FilterableAnimeSeries(s_series, s_date);

        Assert.Equal(Names("Drama"), new TmdbShowGenresSelector().Evaluate(filterable, null, s_date));
        Assert.Equal(Names("Action"), new TmdbMovieGenresSelector().Evaluate(filterable, null, s_date));
        Assert.Equal(Names("Drama", "Action"), new TmdbGenresSelector().Evaluate(filterable, null, s_date));
        Assert.Equal(Names("school"), new TmdbShowKeywordsSelector().Evaluate(filterable, null, s_date));
        Assert.Equal(Names("space"), new TmdbMovieKeywordsSelector().Evaluate(filterable, null, s_date));
        Assert.Equal(Names("school", "space"), new TmdbKeywordsSelector().Evaluate(filterable, null, s_date));
        Assert.True(new HasTmdbGenreExpression("drama").Evaluate(filterable, null, s_date));
        Assert.True(new HasTmdbMovieKeywordExpression("SPACE").Evaluate(filterable, null, s_date));
        Assert.False(new HasTmdbShowKeywordExpression("space").Evaluate(filterable, null, s_date));
    }

    [Fact]
    public void TheEpisodeCountsCountTheLinksMadeToNothingAsTmdbAlwaysDid()
    {
        using var scope = Scope();
        IFilterableInfo filterable = new FilterableAnimeSeries(s_series, s_date);

        // Automatic: the linked episode, the matcher's link to nothing and the film.
        Assert.Equal(3d, new AutomaticTmdbEpisodeLinksSelector().Evaluate(filterable, null, s_date));
        // Verified: the person's link to nothing.
        Assert.Equal(1d, new UserVerifiedTmdbEpisodeLinksSelector().Evaluate(filterable, null, s_date));
        // Missing: only episode 202, since the film stands for 203.
        Assert.Equal(1d, new MissingTmdbEpisodeLinksSelector().Evaluate(filterable, null, s_date));
    }

    [Fact]
    public void AnAnimeWithNoTmdbRowsIsMissingALink()
    {
        using var scope = Scope([], withFilm: false);
        IFilterableInfo filterable = new FilterableAnimeSeries(s_series, s_date);

        Assert.False(new HasTmdbLinkExpression().Evaluate(filterable, null, s_date));
        Assert.True(new MissingTmdbLinkExpression().Evaluate(filterable, null, s_date));
    }

    [Fact]
    public void AnAnimeLinkedToNothingOnPurposeHasALinkAndIsNotMissingOne()
    {
        using var scope = Scope([new() { CrossRef_AniDB_Metadata_SeriesID = 1, Source = MetadataSource.TMDB, AnidbAnimeID = AnidbID, ProviderID = string.Empty }], withFilm: false);
        IFilterableInfo filterable = new FilterableAnimeSeries(s_series, s_date);

        // A link to nothing is still a TMDB row, which HasTmdbLink has always counted.
        Assert.True(new HasTmdbLinkExpression().Evaluate(filterable, null, s_date));
        Assert.False(new MissingTmdbLinkExpression().Evaluate(filterable, null, s_date));
        Assert.Contains(MetadataSource.TMDB, filterable.UnlinkedSources);
        Assert.DoesNotContain(MetadataSource.TMDB, filterable.LinkedSources);
    }

    #endregion
}
