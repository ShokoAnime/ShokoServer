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
using Shoko.Server.Services;
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
    /// The (source, name) pairs offered by the expressions asking whether a
    /// source gives a tag, genre or keyword.
    /// </summary>
    /// <param name="kinds">The kinds of tag to offer.</param>
    /// <returns>The pairs.</returns>
    private static string[][] SourceTagParameterPairs(IReadOnlyCollection<TagKind> kinds)
    {
        var tags = RepoFactory.Metadata_Tag.GetAll()
            .Where(tag => kinds.Contains(tag.Kind))
            .Select(tag => (Source: tag.Source.Value, tag.Name));
        return SourceTagPairs(tags, LinkableSources);
    }

    /// <summary>
    /// The names of TMDB's genres or keywords on its shows, its movies or
    /// both, each once, sorted.
    /// </summary>
    /// <param name="kind">Genres or keywords.</param>
    /// <param name="entityType">Only the shows' or only the movies', or <c>null</c> for both.</param>
    /// <returns>The names.</returns>
    private static string[] TmdbTagNames(TagKind kind, MetadataEntityType? entityType)
    {
        var tags = RepoFactory.Metadata_Tag.GetBySource(MetadataSource.TMDB).Where(tag => tag.Kind == kind).ToList();
        return
        [
            .. tags
                .Where(tag => entityType is null || RepoFactory.Metadata_Tag_Entry.GetByTagID(tag.Metadata_TagID).Any(entry => entry.EntityType == entityType))
                .Select(tag => tag.Name)
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase)
                .Order(StringComparer.InvariantCultureIgnoreCase),
        ];
    }

    /// <summary>
    /// Pairs each source with the tag names it has, once each per source,
    /// in the order of the sources and then by name.
    /// </summary>
    /// <param name="tags">The source value and name of every tag.</param>
    /// <param name="sources">The source values to offer, in order.</param>
    /// <returns>The (source, name) pairs.</returns>
    internal static string[][] SourceTagPairs(IEnumerable<(string Source, string Name)> tags, IEnumerable<string> sources)
    {
        var namesBySource = tags.ToLookup(tag => tag.Source, tag => tag.Name);
        return [.. sources.SelectMany(source => namesBySource[source]
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .Order(StringComparer.InvariantCultureIgnoreCase)
            .Select(name => new[] { source, name }))];
    }

    /// <summary>
    /// Describes every filter expression in the loaded assemblies.
    /// </summary>
    /// <param name="group">Only the expressions of this group, or <c>null</c> for all.</param>
    /// <returns>The help of each expression.</returns>
    /// <exception cref="InvalidOperationException">An expression offers more than one value list for a parameter.</exception>
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

    /// <summary>
    /// Describes one filter expression.
    /// </summary>
    /// <param name="filterType">The expression type.</param>
    /// <returns>The help, or <c>null</c> when the type is no concrete filter expression.</returns>
    /// <exception cref="InvalidOperationException">The expression offers more than one value list for a parameter.</exception>
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
        var possibleParameters = helpParams ?? expression.HelpPossibleParameters;
        var possibleSecondParameters = helpSecondParams ?? expression.HelpPossibleSecondParameters;
        var possibleParameterPairs = helpParamPairs ?? expression.HelpPossibleParameterPairs;
        EnsureOneValueListPerParameter(filterType, possibleParameters, possibleSecondParameters, possibleParameterPairs);
        return new FilterExpressionHelpEntry
        {
            InternalType = filterType,
            Expression = expressionName,
            Name = expression.Name,
            Group = expression.Group,
            Description = expression.HelpDescription,
            PossibleParameters = possibleParameters,
            PossibleSecondParameters = possibleSecondParameters,
            PossibleParameterPairs = possibleParameterPairs,
            ParameterName = expression.HelpParameterName,
            SecondParameterName = expression.HelpSecondParameterName,
            Left = left,
            Right = right,
            Parameter = parameter,
            SecondParameter = secondParameter,
            Type = type,
        };
    }

    /// <summary>
    /// Checks that each parameter of an expression gets one value list at
    /// most: pairs, or a separate list.
    /// </summary>
    /// <param name="filterType">The expression type.</param>
    /// <param name="parameters">The values offered for the first parameter.</param>
    /// <param name="secondParameters">The values offered for the second parameter.</param>
    /// <param name="parameterPairs">The pairs offered for both.</param>
    /// <exception cref="InvalidOperationException">A parameter gets more than one value list.</exception>
    private static void EnsureOneValueListPerParameter(Type filterType, string[]? parameters, string[]? secondParameters, string[][]? parameterPairs)
    {
        var hasPairs = parameterPairs is { Length: > 0 };
        if (hasPairs && (parameters is { Length: > 0 } || secondParameters is { Length: > 0 }))
            throw new InvalidOperationException($"Filter expression {filterType.FullName} offers parameter pairs and a separate parameter list; offer one or the other.");
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

            nameof(HasReleaseProviderNameExpression) =>
            (
                [.. VideoReleaseService.GetProviderNames(RepoFactory.StoredReleaseInfo.GetAll())],
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

            nameof(HasTmdbMovieKeywordExpression) => (TmdbTagNames(TagKind.Keyword, MetadataEntityType.Movie), null, null),

            nameof(HasTmdbMovieGenreExpression) => (TmdbTagNames(TagKind.Genre, MetadataEntityType.Movie), null, null),

            nameof(HasTmdbShowKeywordExpression) => (TmdbTagNames(TagKind.Keyword, MetadataEntityType.Series), null, null),

            nameof(HasTmdbShowGenreExpression) => (TmdbTagNames(TagKind.Genre, MetadataEntityType.Series), null, null),

            nameof(HasTmdbKeywordExpression) => (TmdbTagNames(TagKind.Keyword, null), null, null),

            nameof(HasTmdbGenreExpression) => (TmdbTagNames(TagKind.Genre, null), null, null),

            _ when !TakesSourceParameter(filterType) => (null, null, null),

            nameof(HasSourceGenreExpression) => (null, null, SourceTagParameterPairs([TagKind.Genre])),

            nameof(HasSourceTagExpression) => (null, null, SourceTagParameterPairs([TagKind.Tag, TagKind.Keyword])),

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
        public required string? ParameterName { get; init; }
        public required string? SecondParameterName { get; init; }
    }

    private class SortingExpressionHelpEntry : ISortingExpressionHelp
    {
        public required Type InternalType { get; init; }
        public required string Type { get; init; }
        public required string Name { get; init; }
        public required string Description { get; init; }
    }
}
