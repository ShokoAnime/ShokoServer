using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Filtering.Expressions.Info;
using Shoko.Abstractions.Filtering.Sorting;
using Shoko.Server.Repositories;

namespace Shoko.Server.Filters;

internal static class ExpressionDiscovery
{
    public static IReadOnlyList<IFilterExpressionHelp> GetExpressionHelp(FilterExpressionGroup? group = null)
        => AppDomain.CurrentDomain.GetAssemblies()
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
                RepoFactory.StoredReleaseInfo.GetUsedReleaseGroups()
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

            _ => (null, null, null),
        };

    public static IReadOnlyList<ISortingExpressionHelp> GetSortingExpressionHelp()
        => AppDomain.CurrentDomain.GetAssemblies()
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
