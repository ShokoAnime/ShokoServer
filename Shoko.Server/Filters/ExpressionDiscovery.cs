using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Filtering.Expressions.Info;
using Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;
using Shoko.Abstractions.Filtering.Expressions.Selectors.StringSetSelectors;
using Shoko.Abstractions.Filtering.Sorting;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

namespace Shoko.Server.Filters;

internal static class ExpressionDiscovery
{
    /// <summary>
    /// The sources an entry can be linked to, offered by their values as the
    /// parameter of the expressions that ask about one source.
    /// </summary>
    private static string[] LinkableSources
        => [.. MetadataSource.All.Where(source => source.IsRemote && source != MetadataSource.AniDB).Select(source => source.Value)];

    /// <summary>
    /// The expressions whose first parameter names a source, offered as
    /// <see cref="LinkableSources"/>.
    /// </summary>
    private static readonly FrozenSet<Type> _sourceParameterExpressions = FrozenSet.ToFrozenSet(
    [
        typeof(HasSourceGenreExpression),
        typeof(HasSourceTagExpression),
        typeof(HasSourceLinkExpression),
        typeof(HasAutomaticSourceLinkExpression),
        typeof(MissingSourceLinkExpression),
        typeof(HasSourceAutoLinkingDisabledExpression),
        typeof(HasSourceSuggestionExpression),
        typeof(AutomaticSourceEpisodeLinksSelector),
        typeof(UserVerifiedSourceEpisodeLinksSelector),
        typeof(MissingSourceEpisodeLinksSelector),
        typeof(AutomaticSourceLinksSelector),
        typeof(UserVerifiedSourceLinksSelector),
        typeof(SourceSuggestionCountSelector),
        typeof(SourceGenresSelector),
        typeof(SourceTagsSelector),
    ]);

    /// <summary>
    /// Checks whether an expression's first parameter names a source.
    /// </summary>
    /// <param name="filterType">The expression type.</param>
    /// <returns><c>true</c> when its possible parameters are sources.</returns>
    public static bool TakesSourceParameter(Type filterType)
        => _sourceParameterExpressions.Contains(filterType);

    /// <summary>
    /// Every tag, genre or keyword name any source has stored, TMDB's
    /// included, offered as the second parameter of the expressions that ask
    /// about one.
    /// </summary>
    /// <param name="kinds">The kinds of tag to offer.</param>
    /// <param name="tmdbNames">TMDB's own names of that kind, kept on its models.</param>
    /// <returns>The names, once each.</returns>
    private static string[] StoredTagNames(IReadOnlyCollection<TagKind> kinds, IEnumerable<string> tmdbNames)
        => [.. RepoFactory.Metadata_Tag.GetAll()
            .Where(tag => kinds.Contains(tag.Kind))
            .Select(tag => tag.Name)
            .Concat(tmdbNames)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase)
            .Order(StringComparer.InvariantCultureIgnoreCase)];

    public static IReadOnlyList<IFilterExpressionHelp> GetExpressionHelp(FilterExpressionGroup? group = null)
        => ReflectionUtils.ScannableAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(a =>
                a != typeof(FilterExpression) && !a.IsAbstract && !a.IsGenericType &&
                typeof(FilterExpression).IsAssignableFrom(a) &&
                !typeof(SortingExpression).IsAssignableFrom(a)
            )
            .OrderBy(a => a.FullName)
            .Select(GetExpressionHelp)
            .WhereNotNull()
            .Where(a => group is null || a.Group == group)
            .ToList();

    public static IFilterExpressionHelp? GetExpressionHelp(Type filterType)
    {
        if (filterType == typeof(FilterExpression) || filterType.IsAbstract || filterType.IsGenericType || !typeof(FilterExpression).IsAssignableFrom(filterType) || typeof(SortingExpression).IsAssignableFrom(filterType))
            return null;

        var expression = (FilterExpression?)Activator.CreateInstance(filterType);
        if (expression == null)
            return null;

        FilterExpressionParameterType? left = expression switch
        {
            IWithExpressionParameter => FilterExpressionParameterType.Expression,
            IWithDateSelectorParameter => FilterExpressionParameterType.DateSelector,
            IWithNumberSelectorParameter => FilterExpressionParameterType.NumberSelector,
            IWithStringSelectorParameter => FilterExpressionParameterType.StringSelector,
            IWithStringSetSelectorParameter => FilterExpressionParameterType.StringSetSelector,
            _ => null,
        };

        FilterExpressionParameterType? right = expression switch
        {
            IWithSecondExpressionParameter => FilterExpressionParameterType.Expression,
            IWithSecondDateSelectorParameter => FilterExpressionParameterType.DateSelector,
            IWithSecondNumberSelectorParameter => FilterExpressionParameterType.NumberSelector,
            IWithSecondStringSelectorParameter => FilterExpressionParameterType.StringSelector,
            _ => null,
        };

        FilterExpressionParameterType? parameter = expression switch
        {
            IWithBoolParameter => FilterExpressionParameterType.Bool,
            IWithDateParameter => FilterExpressionParameterType.Date,
            IWithNumberParameter => FilterExpressionParameterType.Number,
            IWithStringParameter => FilterExpressionParameterType.String,
            IWithStringSetParameter => FilterExpressionParameterType.StringSet,
            IWithTimeSpanParameter => FilterExpressionParameterType.TimeSpan,
            _ => null,
        };

        FilterExpressionParameterType? secondParameter = expression switch
        {
            IWithSecondStringParameter => FilterExpressionParameterType.String,
            _ => null,
        };

        var type = expression switch
        {
            FilterExpression<bool> => FilterExpressionParameterType.Expression,
            FilterExpression<DateTime?> => FilterExpressionParameterType.DateSelector,
            FilterExpression<double> => FilterExpressionParameterType.NumberSelector,
            FilterExpression<string> => FilterExpressionParameterType.StringSelector,
            FilterExpression<IReadOnlySet<string>> => FilterExpressionParameterType.StringSetSelector,
            _ => throw new Exception($"Expression {filterType.Name} is not a handled type for Filter Expression Help")
        };

        var expressionName = filterType.Name.TrimEnd("Expression").TrimEnd("Function").TrimEnd("Selector").Trim();
        var (helpParams, helpSecondParams, helpParamPairs) = GetHelpParameterOverrides(filterType);
        return new FilterExpressionHelpEntry
        {
            InternalType = filterType,
            Expression = expressionName,
            Name = expression.Name,
            Group = expression.Group,
            Description = expression.HelpDescription,
            PossibleParameters = helpParams ?? expression.HelpPossibleParameters,
            PossibleSecondParameters = helpSecondParams ?? expression.HelpPossibleSecondParameters,
            PossibleParameterPairs = helpParamPairs ?? expression.HelpPossibleParameterPairs,
            Left = left,
            Right = right,
            Parameter = parameter,
            SecondParameter = secondParameter,
            Type = type,
        };
    }

    /// <summary>
    /// Returns server-side help parameter overrides for expressions whose
    /// parameter lists cannot be computed in the abstractions layer (because
    /// they depend on repositories or other server services).
    /// </summary>
    private static (string[]? Parameters, string[]? SecondParameters, string[][]? ParameterPairs) GetHelpParameterOverrides(Type filterType)
        => filterType.Name switch
        {
            nameof(InYearExpression) =>
            (
                RepoFactory.AnimeSeries.GetAllYears()
                    .Select(a => a.ToString())
                    .ToArray(),
                null,
                null
            ),

            nameof(InSeasonExpression) =>
            (
                null,
                null,
                RepoFactory.AnimeSeries.GetAllSeasons()
                    .Select(a => new[] { a.Year.ToString(), a.Season.ToString() })
                    .ToArray()
            ),

            nameof(HasTagExpression) =>
            (
                RepoFactory.AniDB_Tag?.GetAllForLocalSeries()
                    .Select(a => a.TagName.Replace('`', '\''))
                    .ToArray() ?? [],
                null,
                null
            ),

            nameof(HasAvailableImageExpression) or nameof(HasPreferredImageExpression) =>
            (
                RepoFactory.AnimeSeries.GetAllImageTypes()
                    .Select(a => a.ToString())
                    .ToArray(),
                null,
                null
            ),

            nameof(HasReleaseGroupNameExpression) =>
            (
                RepoFactory.StoredReleaseInfo.GetUsedReleaseGroups(null)
                    .Select(r => r.Name)
                    .ToArray(),
                null,
                null
            ),

            nameof(HasTmdbMovieKeywordExpression) =>
            (
                RepoFactory.TMDB_Movie.GetAllKeywords().ToArray(),
                null,
                null
            ),

            nameof(HasTmdbMovieGenreExpression) =>
            (
                RepoFactory.TMDB_Movie.GetAllGenres().ToArray(),
                null,
                null
            ),

            nameof(HasTmdbShowKeywordExpression) =>
            (
                RepoFactory.TMDB_Show.GetAllKeywords().ToArray(),
                null,
                null
            ),

            nameof(HasTmdbShowGenreExpression) =>
            (
                RepoFactory.TMDB_Show.GetAllGenres().ToArray(),
                null,
                null
            ),

            nameof(HasTmdbKeywordExpression) =>
            (
                RepoFactory.TMDB_Movie.GetAllKeywords()
                    .Concat(RepoFactory.TMDB_Show.GetAllKeywords())
                    .ToHashSet(StringComparer.InvariantCultureIgnoreCase)
                    .ToArray(),
                null,
                null
            ),

            nameof(HasTmdbGenreExpression) =>
            (
                RepoFactory.TMDB_Movie.GetAllGenres()
                    .Concat(RepoFactory.TMDB_Show.GetAllGenres())
                    .ToHashSet(StringComparer.InvariantCultureIgnoreCase)
                    .ToArray(),
                null,
                null
            ),

            _ when !TakesSourceParameter(filterType) => (null, null, null),

            nameof(HasSourceGenreExpression) =>
            (
                LinkableSources,
                StoredTagNames([TagKind.Genre], RepoFactory.TMDB_Movie.GetAllGenres().Concat(RepoFactory.TMDB_Show.GetAllGenres())),
                null
            ),

            nameof(HasSourceTagExpression) =>
            (
                LinkableSources,
                StoredTagNames([TagKind.Tag, TagKind.Keyword], RepoFactory.TMDB_Movie.GetAllKeywords().Concat(RepoFactory.TMDB_Show.GetAllKeywords())),
                null
            ),

            _ =>
            (
                LinkableSources,
                null,
                null
            ),
        };

    public static IReadOnlyList<ISortingExpressionHelp> GetSortingExpressionHelp()
        => ReflectionUtils.ScannableAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(a => a != typeof(FilterExpression) && !a.IsAbstract && !a.IsGenericType &&
                typeof(SortingExpression).IsAssignableFrom(a)
            )
            .OrderBy(a => a.FullName)
            .Select(GetSortingExpressionHelp)
            .WhereNotNull()
            .ToList();

    public static ISortingExpressionHelp? GetSortingExpressionHelp(Type sortingType)
    {
        if (sortingType == typeof(FilterExpression) || sortingType.IsAbstract || sortingType.IsGenericType || !typeof(SortingExpression).IsAssignableFrom(sortingType))
            return null;

        var criteria = (SortingExpression?)Activator.CreateInstance(sortingType);
        if (criteria == null)
            return null;

        return new SortingExpressionHelpEntry
        {
            InternalType = sortingType,
            Type = sortingType.Name.TrimEnd("SortingSelector").Trim(),
            Name = criteria.Name,
            Description = criteria.HelpDescription,
        };
    }

    private class FilterExpressionHelpEntry : IFilterExpressionHelp
    {
        public required Type InternalType { get; init; }
        public required string Expression { get; init; }
        public required string Name { get; init; }
        public required FilterExpressionGroup Group { get; init; }
        public required string Description { get; init; }
        public required FilterExpressionParameterType Type { get; init; }
        public required FilterExpressionParameterType? Left { get; init; }
        public required FilterExpressionParameterType? Right { get; init; }
        public required FilterExpressionParameterType? Parameter { get; init; }
        public required FilterExpressionParameterType? SecondParameter { get; init; }
        public required string[]? PossibleParameters { get; init; }
        public required string[]? PossibleSecondParameters { get; init; }
        public required string[][]? PossibleParameterPairs { get; init; }
    }

    private class SortingExpressionHelpEntry : ISortingExpressionHelp
    {
        public required Type InternalType { get; init; }
        public required string Type { get; init; }
        public required string Name { get; init; }
        public required string Description { get; init; }
    }
}
