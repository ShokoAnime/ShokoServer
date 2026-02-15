using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.API.v1.Models;
using Shoko.Server.Filters.Files;
using Shoko.Server.Filters.Functions;
using Shoko.Server.Filters.Info;
using Shoko.Server.Filters.Interfaces;
using Shoko.Server.Filters.Logic.DateTimes;
using Shoko.Server.Filters.Logic.Expressions;
using Shoko.Server.Filters.Logic.Numbers;
using Shoko.Server.Filters.Selectors.DateSelectors;
using Shoko.Server.Filters.Selectors.NumberSelectors;
using Shoko.Server.Filters.SortingSelectors;
using Shoko.Server.Filters.User;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Filters.Legacy;

public static class LegacyConditionConverter
{
    public static bool TryConvertToConditions(FilterPreset filter, out List<CL_GroupFilterCondition> conditions, out CL_GroupFilterBaseCondition baseCondition)
    {
        // The allowed conversions are:
        // Not(...) -> BaseCondition Inverted
        // And(And(And(...))) -> Chains of And become the list
        // a single condition

        var expression = filter.Expression;
        // treat null expression similar to All
        if (expression == null)
        {
            conditions = [];
            baseCondition = CL_GroupFilterBaseCondition.Include;
            return true;
        }

        if (TryGetSingleCondition(expression, out var condition))
        {
            baseCondition = CL_GroupFilterBaseCondition.Include;
            conditions = [condition];
            return true;
        }

        var results = new List<CL_GroupFilterCondition>();
        if (expression is NotExpression not)
        {
            baseCondition = CL_GroupFilterBaseCondition.Exclude;
            if (TryGetConditionsRecursive<OrExpression>(not.Left, results))
            {
                conditions = results;
                return true;
            }
        }

        baseCondition = CL_GroupFilterBaseCondition.Include;
        if (TryGetConditionsRecursive<AndExpression>(expression, results))
        {
            conditions = results;
            return true;
        }

        conditions = null;
        return false;
    }

    private static bool TryGetSingleCondition(FilterExpression expression, out CL_GroupFilterCondition condition)
    {
        if (TryGetGroupCondition(expression, out condition)) return true;
        if (TryGetIncludeCondition(expression, out condition)) return true;
        if (TryGetInCondition(expression, out condition)) return true;
        return TryGetComparatorCondition(expression, out condition);
    }

    private static bool TryGetConditionsRecursive<T>(FilterExpression expression, List<CL_GroupFilterCondition> conditions) where T : IWithExpressionParameter, IWithSecondExpressionParameter
    {
        // Do this first, as compound expressions can throw off the following logic
        if (TryGetSingleCondition(expression, out var condition))
        {
            conditions.Add(condition);
            return true;
        }

        if (expression is T and) return TryGetConditionsRecursive<T>(and.Left, conditions) && TryGetConditionsRecursive<T>(and.Right, conditions);
        return false;
    }

    private static bool TryGetGroupCondition(FilterExpression expression, out CL_GroupFilterCondition condition)
    {
        var conditionOperator = CL_GroupFilterOperator.Equals;
        if (expression is NotExpression not)
        {
            conditionOperator = CL_GroupFilterOperator.Exclude;
            expression = not.Left;
        }

        if (expression is not HasNameExpression nameExpression)
        {
            condition = null;
            return false;
        }

        condition = new CL_GroupFilterCondition
        {
            ConditionType = (int)CL_GroupFilterConditionType.AnimeGroup,
            ConditionOperator = (int)conditionOperator,
            ConditionParameter = nameExpression.Parameter,
        };
        return true;
    }

    private static bool TryGetIncludeCondition(FilterExpression expression, out CL_GroupFilterCondition condition)
    {
        var conditionOperator = CL_GroupFilterOperator.Include;
        if (expression is NotExpression not)
        {
            conditionOperator = CL_GroupFilterOperator.Exclude;
            expression = not.Left;
        }

        condition = null;
        var type = expression.GetType();
        if (type == typeof(HasMissingEpisodesExpression))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.MissingEpisodes,
            };
            return true;
        }

        if (type == typeof(HasMissingEpisodesCollectingExpression))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.MissingEpisodesCollecting,
            };
            return true;
        }

        if (type == typeof(HasUnwatchedEpisodesExpression))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.HasUnwatchedEpisodes,
            };
            return true;
        }

        if (type == typeof(HasWatchedEpisodesExpression))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.HasWatchedEpisodes,
            };
            return true;
        }

        if (type == typeof(HasPermanentUserVotesExpression))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.UserVoted,
            };
            return true;
        }

        if (type == typeof(HasUserVotesExpression))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.UserVotedAny,
            };
            return true;
        }

        if (type == typeof(HasTvDBLinkExpression))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.AssignedTvDBInfo,
            };
            return true;
        }

        if (type == typeof(HasTmdbLinkExpression))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.AssignedMovieDBInfo,
            };
            return true;
        }

        if (type == typeof(HasTraktLinkExpression))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.AssignedTraktInfo,
            };
            return true;
        }

        if (type == typeof(IsFavoriteExpression))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.Favourite,
            };
            return true;
        }

        if (type == typeof(IsFinishedExpression))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.FinishedAiring,
            };
            return true;
        }

        if (type == typeof(HasTmdbLinkExpression))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.AssignedMovieDBInfo,
            };
            return true;
        }

        if (expression == LegacyMappings.GetCompletedExpression())
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.CompletedSeries,
            };
            return true;
        }

        if (expression == new OrExpression(new HasTvDBLinkExpression(), new HasTmdbLinkExpression()))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.AssignedTvDBOrMovieDBInfo,
            };
            return true;
        }

        return false;
    }

    private static bool TryGetInCondition(FilterExpression expression, out CL_GroupFilterCondition condition)
    {
        var conditionOperator = CL_GroupFilterOperator.In;
        if (expression is NotExpression not)
        {
            conditionOperator = CL_GroupFilterOperator.NotIn;
            expression = not.Left;
        }

        if (IsInTag(expression, out var tags))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.Tag,
                ConditionParameter = string.Join(",", tags)
            };
            return true;
        }

        if (IsInCustomTag(expression, out var customTags))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.CustomTags,
                ConditionParameter = string.Join(",", customTags)
            };
            return true;
        }

        if (IsInAnimeType(expression, out var animeType))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.AnimeType,
                ConditionParameter = string.Join(",", animeType)
            };
            return true;
        }

        if (IsInVideoQuality(expression, out var videoQualities))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.VideoQuality,
                ConditionParameter = string.Join(",", videoQualities)
            };
            return true;
        }

        if (IsInGroup(expression, out var groups))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.AnimeGroup,
                ConditionParameter = string.Join(",", groups)
            };
            return true;
        }

        if (IsInYear(expression, out var years))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.Year,
                ConditionParameter = string.Join(",", years)
            };
            return true;
        }

        if (IsInSeason(expression, out var seasons))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.Season,
                ConditionParameter = string.Join(",", seasons.Select(a => a.Season + " " + a.Year))
            };
            return true;
        }

        if (IsInAudioLanguage(expression, out var aLanguages))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.AudioLanguage,
                ConditionParameter = string.Join(",", aLanguages)
            };
            return true;
        }

        if (IsInSubtitleLanguage(expression, out var sLanguages))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)conditionOperator,
                ConditionType = (int)CL_GroupFilterConditionType.SubtitleLanguage,
                ConditionParameter = string.Join(",", sLanguages)
            };
            return true;
        }

        if (IsInSharedVideoQuality(expression, out var sVideoQuality))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = conditionOperator == CL_GroupFilterOperator.NotIn ? (int)CL_GroupFilterOperator.NotInAllEpisodes : (int)CL_GroupFilterOperator.InAllEpisodes,
                ConditionType = (int)CL_GroupFilterConditionType.VideoQuality,
                ConditionParameter = string.Join(",", sVideoQuality)
            };
            return true;
        }

        if (IsInSharedAudioLanguage(expression, out var sALanguages))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = conditionOperator == CL_GroupFilterOperator.NotIn ? (int)CL_GroupFilterOperator.NotInAllEpisodes : (int)CL_GroupFilterOperator.InAllEpisodes,
                ConditionType = (int)CL_GroupFilterConditionType.AudioLanguage,
                ConditionParameter = string.Join(",", sALanguages)
            };
            return true;
        }

        if (IsInSharedSubtitleLanguage(expression, out var sSLanguages))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = conditionOperator == CL_GroupFilterOperator.NotIn ? (int)CL_GroupFilterOperator.NotInAllEpisodes : (int)CL_GroupFilterOperator.InAllEpisodes,
                ConditionType = (int)CL_GroupFilterConditionType.SubtitleLanguage,
                ConditionParameter = string.Join(",", sSLanguages)
            };
            return true;
        }

        condition = null;
        return false;
    }

    private static bool TryGetComparatorCondition(FilterExpression expression, out CL_GroupFilterCondition condition)
    {
        condition = null;
        if (IsAirDate(expression, out var airDatePara, out var airDateOperator))
        {
            var para = airDatePara is DateTime date ? date.ToString("yyyyMMdd") : airDatePara.ToString();
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)airDateOperator,
                ConditionType = (int)CL_GroupFilterConditionType.AirDate,
                ConditionParameter = para
            };
            return true;
        }

        if (IsLatestAirDate(expression, out var lastAirDatePara, out var lastAirDateOperator))
        {
            var para = lastAirDatePara is DateTime date ? date.ToString("yyyyMMdd") : lastAirDatePara.ToString();
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)lastAirDateOperator,
                ConditionType = (int)CL_GroupFilterConditionType.LatestEpisodeAirDate,
                ConditionParameter = para
            };
            return true;
        }

        if (IsSeriesCreatedDate(expression, out var seriesCreatedDatePara, out var seriesCreatedDateOperator))
        {
            var para = seriesCreatedDatePara is DateTime date ? date.ToString("yyyyMMdd") : seriesCreatedDatePara.ToString();
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)seriesCreatedDateOperator,
                ConditionType = (int)CL_GroupFilterConditionType.SeriesCreatedDate,
                ConditionParameter = para
            };
            return true;
        }

        if (IsEpisodeAddedDate(expression, out var episodeAddedDatePara, out var episodeAddedDateOperator))
        {
            var para = episodeAddedDatePara is DateTime date ? date.ToString("yyyyMMdd") : episodeAddedDatePara.ToString();
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)episodeAddedDateOperator,
                ConditionType = (int)CL_GroupFilterConditionType.EpisodeAddedDate,
                ConditionParameter = para
            };
            return true;
        }

        if (IsEpisodeWatchedDate(expression, out var episodeWatchedDatePara, out var episodeWatchedDateOperator))
        {
            var para = episodeWatchedDatePara is DateTime date ? date.ToString("yyyyMMdd") : episodeWatchedDatePara.ToString();
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)episodeWatchedDateOperator,
                ConditionType = (int)CL_GroupFilterConditionType.EpisodeWatchedDate,
                ConditionParameter = para
            };
            return true;
        }

        if (IsAniDBRating(expression, out var aniDBRatingPara, out var aniDBRatingOperator))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)aniDBRatingOperator,
                ConditionType = (int)CL_GroupFilterConditionType.AniDBRating,
                ConditionParameter = aniDBRatingPara.ToString()
            };
            return true;
        }

        if (IsUserRating(expression, out var userRatingPara, out var userRatingOperator))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)userRatingOperator,
                ConditionType = (int)CL_GroupFilterConditionType.UserRating,
                ConditionParameter = userRatingPara.ToString()
            };
            return true;
        }

        if (IsEpisodeCount(expression, out var episodeCountPara, out var episodeCountOperator))
        {
            condition = new CL_GroupFilterCondition
            {
                ConditionOperator = (int)episodeCountOperator,
                ConditionType = (int)CL_GroupFilterConditionType.EpisodeCount,
                ConditionParameter = episodeCountPara.ToString()
            };
            return true;
        }

        return false;
    }

    private static bool IsInTag(FilterExpression expression, out List<string> parameters)
    {
        parameters = [];
        return TryParseIn(expression, typeof(HasTagExpression), parameters);
    }

    private static bool IsInCustomTag(FilterExpression expression, out List<string> parameters)
    {
        parameters = [];
        return TryParseIn(expression, typeof(HasCustomTagExpression), parameters);
    }

    private static bool IsInAnimeType(FilterExpression expression, out List<string> parameters)
    {
        parameters = [];
        return TryParseIn(expression, typeof(HasAnimeTypeExpression), parameters);
    }

    private static bool IsInVideoQuality(FilterExpression expression, out List<string> parameters)
    {
        parameters = [];
        return TryParseIn(expression, typeof(HasVideoSourceExpression), parameters);
    }

    private static bool IsInSharedVideoQuality(FilterExpression expression, out List<string> parameters)
    {
        parameters = [];
        return TryParseIn(expression, typeof(HasSharedVideoSourceExpression), parameters);
    }

    private static bool IsInGroup(FilterExpression expression, out List<string> parameters)
    {
        parameters = [];
        return TryParseIn(expression, typeof(HasNameExpression), parameters);
    }

    private static bool IsInYear(FilterExpression expression, out List<int> parameters)
    {
        parameters = [];
        return TryParseIn(expression, typeof(InYearExpression), parameters);
    }

    private static bool IsInSeason(FilterExpression expression, out List<(int Year, string Season)> parameters)
    {
        parameters = [];
        return TryParseIn(expression, typeof(InSeasonExpression), parameters);
    }

    private static bool IsInAudioLanguage(FilterExpression expression, out List<string> parameters)
    {
        parameters = [];
        return TryParseIn(expression, typeof(HasAudioLanguageExpression), parameters);
    }

    private static bool IsInSubtitleLanguage(FilterExpression expression, out List<string> parameters)
    {
        parameters = [];
        return TryParseIn(expression, typeof(HasSubtitleLanguageExpression), parameters);
    }

    private static bool IsInSharedAudioLanguage(FilterExpression expression, out List<string> parameters)
    {
        parameters = [];
        return TryParseIn(expression, typeof(HasSharedAudioLanguageExpression), parameters);
    }

    private static bool IsInSharedSubtitleLanguage(FilterExpression expression, out List<string> parameters)
    {
        parameters = [];
        return TryParseIn(expression, typeof(HasSharedSubtitleLanguageExpression), parameters);
    }

    private static bool IsAirDate(FilterExpression expression, out object parameter, out CL_GroupFilterOperator gfOperator)
    {
        return TryParseComparator(expression, typeof(AirDateSelector), out parameter, out gfOperator);
    }

    private static bool IsLatestAirDate(FilterExpression expression, out object parameter, out CL_GroupFilterOperator gfOperator)
    {
        return TryParseComparator(expression, typeof(LastAirDateSelector), out parameter, out gfOperator);
    }

    private static bool IsSeriesCreatedDate(FilterExpression expression, out object parameter, out CL_GroupFilterOperator gfOperator)
    {
        return TryParseComparator(expression, typeof(AddedDateSelector), out parameter, out gfOperator);
    }

    private static bool IsEpisodeAddedDate(FilterExpression expression, out object parameter, out CL_GroupFilterOperator gfOperator)
    {
        return TryParseComparator(expression, typeof(LastAddedDateSelector), out parameter, out gfOperator);
    }

    private static bool IsEpisodeWatchedDate(FilterExpression expression, out object parameter, out CL_GroupFilterOperator gfOperator)
    {
        return TryParseComparator(expression, typeof(LastWatchedDateSelector), out parameter, out gfOperator);
    }

    private static bool IsAniDBRating(FilterExpression expression, out object parameter, out CL_GroupFilterOperator gfOperator)
    {
        return TryParseComparator(expression, typeof(AverageAniDBRatingSelector), out parameter, out gfOperator) ||
               TryParseComparator(expression, typeof(HighestAniDBRatingSelector), out parameter, out gfOperator) ||
               TryParseComparator(expression, typeof(LowestAniDBRatingSelector), out parameter, out gfOperator);
    }

    private static bool IsUserRating(FilterExpression expression, out object parameter, out CL_GroupFilterOperator gfOperator)
    {
        return TryParseComparator(expression, typeof(HighestUserRatingSelector), out parameter, out gfOperator);
    }

    private static bool IsEpisodeCount(FilterExpression expression, out object parameter, out CL_GroupFilterOperator gfOperator)
    {
        return TryParseComparator(expression, typeof(EpisodeCountSelector), out parameter, out gfOperator);
    }

    private static bool TryParseIn<T>(FilterExpression expression, Type type, List<T> parameters)
    {
        if (expression is OrExpression or) return TryParseIn(or.Left, type, parameters) && TryParseIn(or.Right, type, parameters);
        if (expression.GetType() != type) return false;

        if (typeof(T) == typeof(string) && expression is IWithStringParameter withString) parameters.Add((T)(object)withString.Parameter);
        else if (typeof(T) == typeof(DateTime) && expression is IWithDateParameter withDate) parameters.Add((T)(object)withDate.Parameter);
        else if (typeof(T) == typeof(double) && expression is IWithNumberParameter withNumber) parameters.Add((T)(object)withNumber.Parameter);
        else if (typeof(T) == typeof(TimeSpan) && expression is IWithTimeSpanParameter withTimeSpan) parameters.Add((T)(object)withTimeSpan.Parameter);

        return true;

    }

    private static bool TryParseIn<T, T1>(FilterExpression expression, Type type, List<(T, T1)> parameters)
    {
        if (expression is OrExpression or) return TryParseIn(or.Left, type, parameters) && TryParseIn(or.Right, type, parameters);
        if (expression.GetType() != type) return false;

        T first = default;
        T1 second = default;
        if (typeof(T) == typeof(string) && expression is IWithStringParameter withString) first = (T)(object)withString.Parameter;
        else if (typeof(T) == typeof(DateTime) && expression is IWithDateParameter withDate) first = (T)(object)withDate.Parameter;
        else if (typeof(T) == typeof(int) && expression is IWithNumberParameter withInt) first = (T)(object)Convert.ToInt32(withInt.Parameter);
        else if (typeof(T) == typeof(double) && expression is IWithNumberParameter withNumber) first = (T)(object)withNumber.Parameter;
        else if (typeof(T) == typeof(TimeSpan) && expression is IWithTimeSpanParameter withTimeSpan) first = (T)(object)withTimeSpan.Parameter;
        if (typeof(T1) == typeof(string) && expression is IWithSecondStringParameter withSecondString) second = (T1)(object)withSecondString.SecondParameter;
        if (!EqualityComparer<T>.Default.Equals(first, default) && !EqualityComparer<T1>.Default.Equals(second, default)) parameters.Add((first, second));

        return true;

    }

    private static bool TryParseComparator(FilterExpression expression, Type type, out object parameter, out CL_GroupFilterOperator gfOperator)
    {
        // comparators share a similar format:
        // Expression(selector, selector) or Expression(selector, parameter)
        gfOperator = 0;
        parameter = null;
        switch (expression)
        {
            // These are inverted because the parameter is second, as compared to the Legacy method, which evaluated as parameter operator selector
            case DateGreaterThanExpression dateGreater when dateGreater.Left?.GetType() != type:
                return false;
            case DateGreaterThanExpression dateGreater:
                gfOperator = CL_GroupFilterOperator.LessThan;
                parameter = dateGreater.Parameter;
                return true;
            case DateGreaterThanEqualsExpression dateGreaterEquals when dateGreaterEquals.Left?.GetType() != type:
                return false;
            case DateGreaterThanEqualsExpression dateGreaterEquals:
            {
                if (dateGreaterEquals.Right is not DateDiffFunction f) return false;
                if (f.Left is not DateAddFunction add) return false;
                if (add.Left is not TodayFunction) return false;
                if (add.Parameter != TimeSpan.FromDays(1) - TimeSpan.FromMilliseconds(1)) return false;
                gfOperator = CL_GroupFilterOperator.LastXDays;
                parameter = f.Parameter.TotalDays;
                return true;
            }
            case NumberGreaterThanExpression numberGreater when numberGreater.Left?.GetType() != type:
                return false;
            case NumberGreaterThanExpression numberGreater:
                gfOperator = CL_GroupFilterOperator.LessThan;
                parameter = numberGreater.Parameter;
                return true;
            case DateLessThanExpression dateLess when dateLess.Left?.GetType() != type:
                return false;
            case DateLessThanExpression dateLess:
                gfOperator = CL_GroupFilterOperator.GreaterThan;
                parameter = dateLess.Parameter;
                return true;
            case NumberLessThanExpression numberLess when numberLess.Left?.GetType() != type:
                return false;
            case NumberLessThanExpression numberLess:
                gfOperator = CL_GroupFilterOperator.GreaterThan;
                parameter = numberLess.Parameter;
                return true;
            default:
                return false;
        }
    }

    public static string GetSortingCriteria(FilterPreset filterPreset)
    {
        return string.Join("|", GetSortingCriteriaList(filterPreset).Select(a => (int)a.SortType + ";" + (int)a.SortDirection));
    }

    public static List<LegacyGroupFilterSortingCriteria> GetSortingCriteriaList(FilterPreset filter)
    {
        var results = new List<LegacyGroupFilterSortingCriteria>();
        var expression = filter.SortingExpression;
        if (expression == null)
        {
            results.Add(new LegacyGroupFilterSortingCriteria
            {
                GroupFilterID = filter.FilterPresetID,
                SortType = CL_GroupFilterSorting.GroupName
            });
            return results;
        }

        var current = expression;
        while (current != null)
        {
            var type = current.GetType();
            CL_GroupFilterSorting sortType = 0;
            if (type == typeof(AddedDateSortingSelector))
                sortType = CL_GroupFilterSorting.SeriesAddedDate;
            else if (type == typeof(LastAddedDateSortingSelector))
                sortType = CL_GroupFilterSorting.EpisodeAddedDate;
            else if (type == typeof(LastAirDateSortingSelector))
                sortType = CL_GroupFilterSorting.EpisodeAirDate;
            else if (type == typeof(LastWatchedDateSortingSelector))
                sortType = CL_GroupFilterSorting.EpisodeWatchedDate;
            else if (type == typeof(NameSortingSelector))
                sortType = CL_GroupFilterSorting.GroupName;
            else if (type == typeof(AirDateSortingSelector))
                sortType = CL_GroupFilterSorting.Year;
            else if (type == typeof(SeriesCountSortingSelector))
                sortType = CL_GroupFilterSorting.SeriesCount;
            else if (type == typeof(UnwatchedEpisodeCountSortingSelector))
                sortType = CL_GroupFilterSorting.UnwatchedEpisodeCount;
            else if (type == typeof(MissingEpisodeCountSortingSelector))
                sortType = CL_GroupFilterSorting.MissingEpisodeCount;
            else if (type == typeof(HighestUserRatingSortingSelector))
                sortType = CL_GroupFilterSorting.UserRating;
            else if (type == typeof(LowestUserRatingSortingSelector))
                sortType = CL_GroupFilterSorting.UserRating;
            else if (type == typeof(AverageAniDBRatingSortingSelector))
                sortType = CL_GroupFilterSorting.AniDBRating;
            else if (type == typeof(HighestAniDBRatingSortingSelector))
                sortType = CL_GroupFilterSorting.AniDBRating;
            else if (type == typeof(LowestAniDBRatingSortingSelector))
                sortType = CL_GroupFilterSorting.AniDBRating;
            else if (type == typeof(SortingNameSortingSelector))
                sortType = CL_GroupFilterSorting.SortName;

            if (sortType != 0)
            {
                results.Add(new LegacyGroupFilterSortingCriteria
                {
                    GroupFilterID = filter.FilterPresetID,
                    SortType = sortType,
                    SortDirection = current.Descending ? CL_GroupFilterSortDirection.Desc : CL_GroupFilterSortDirection.Asc
                });
            }
            current = current.Next;
        }

        return results;
    }

    public static FilterExpression<bool> GetExpression(List<CL_GroupFilterCondition> conditions, CL_GroupFilterBaseCondition baseCondition, bool suppressErrors = false)
    {
        // forward compatibility is easier. Just map the old conditions to an expression
        if (conditions == null || conditions.Count < 1) return null;
        var first = conditions.Select((a, index) => new { Expression = GetExpression(a, suppressErrors), Index = index }).FirstOrDefault(a => a.Expression != null);
        if (first == null) return null;
        if (baseCondition == CL_GroupFilterBaseCondition.Exclude)
        {
            return new NotExpression(conditions.Count == 1 ? first.Expression : conditions.Skip(first.Index + 1).Aggregate(first.Expression, (a, b) =>
            {
                var result = GetExpression(b, suppressErrors);
                return result == null ? a : new OrExpression(a, result);
            }));
        }

        return conditions.Count == 1 ? first.Expression : conditions.Skip(first.Index + 1).Aggregate(first.Expression, (a, b) =>
        {
            var result = GetExpression(b, suppressErrors);
            return result == null ? a : new AndExpression(a, result);
        });
    }

    private static FilterExpression<bool> GetExpression(CL_GroupFilterCondition condition, bool suppressErrors = false)
    {
        var op = (CL_GroupFilterOperator)condition.ConditionOperator;
        var parameter = condition.ConditionParameter;
        switch ((CL_GroupFilterConditionType)condition.ConditionType)
        {
            case CL_GroupFilterConditionType.CompletedSeries:
                if (op == CL_GroupFilterOperator.Include)
                    return LegacyMappings.GetCompletedExpression();
                return new NotExpression(LegacyMappings.GetCompletedExpression());
            case CL_GroupFilterConditionType.FinishedAiring:
                if (op == CL_GroupFilterOperator.Include)
                    return new IsFinishedExpression();
                return new NotExpression(new IsFinishedExpression());
            case CL_GroupFilterConditionType.MissingEpisodes:
                if (op == CL_GroupFilterOperator.Include)
                    return new HasMissingEpisodesExpression();
                return new NotExpression(new HasMissingEpisodesExpression());
            case CL_GroupFilterConditionType.MissingEpisodesCollecting:
                if (op == CL_GroupFilterOperator.Include)
                    return new HasMissingEpisodesCollectingExpression();
                return new NotExpression(new HasMissingEpisodesCollectingExpression());
            case CL_GroupFilterConditionType.HasUnwatchedEpisodes:
                if (op == CL_GroupFilterOperator.Include)
                    return new HasUnwatchedEpisodesExpression();
                return new NotExpression(new HasUnwatchedEpisodesExpression());
            case CL_GroupFilterConditionType.HasWatchedEpisodes:
                if (op == CL_GroupFilterOperator.Include)
                    return new HasWatchedEpisodesExpression();
                return new NotExpression(new HasWatchedEpisodesExpression());
            case CL_GroupFilterConditionType.UserVoted:
                if (op == CL_GroupFilterOperator.Include)
                    return new HasPermanentUserVotesExpression();
                return new NotExpression(new HasPermanentUserVotesExpression());
            case CL_GroupFilterConditionType.UserVotedAny:
                if (op == CL_GroupFilterOperator.Include)
                    return new HasUserVotesExpression();
                return new NotExpression(new HasUserVotesExpression());
            case CL_GroupFilterConditionType.Favourite:
                if (op == CL_GroupFilterOperator.Include)
                    return new IsFavoriteExpression();
                return new NotExpression(new IsFavoriteExpression());
            case CL_GroupFilterConditionType.AssignedTvDBInfo:
                if (op == CL_GroupFilterOperator.Include)
                    return new HasTvDBLinkExpression();
                return new NotExpression(new HasTvDBLinkExpression());
            case CL_GroupFilterConditionType.AssignedMovieDBInfo:
                if (op == CL_GroupFilterOperator.Include)
                    return new HasTmdbLinkExpression();
                return new NotExpression(new HasTmdbLinkExpression());
            case CL_GroupFilterConditionType.AssignedTvDBOrMovieDBInfo:
                if (op == CL_GroupFilterOperator.Include)
                    return new OrExpression(new HasTvDBLinkExpression(), new HasTmdbLinkExpression());
                return new NotExpression(new OrExpression(new HasTvDBLinkExpression(), new HasTmdbLinkExpression()));
            case CL_GroupFilterConditionType.AssignedTraktInfo:
                if (op == CL_GroupFilterOperator.Include)
                    return new HasTraktLinkExpression();
                return new NotExpression(new HasTraktLinkExpression());
            case CL_GroupFilterConditionType.Tag:
                return LegacyMappings.GetTagExpression(op, parameter, suppressErrors);
            case CL_GroupFilterConditionType.CustomTags:
                return LegacyMappings.GetCustomTagExpression(op, parameter, suppressErrors);
            case CL_GroupFilterConditionType.AirDate:
                return LegacyMappings.GetAirDateExpression(op, parameter, suppressErrors);
            case CL_GroupFilterConditionType.LatestEpisodeAirDate:
                return LegacyMappings.GetLastAirDateExpression(op, parameter, suppressErrors);
            case CL_GroupFilterConditionType.SeriesCreatedDate:
                return LegacyMappings.GetAddedDateExpression(op, parameter, suppressErrors);
            case CL_GroupFilterConditionType.EpisodeAddedDate:
                return LegacyMappings.GetLastAddedDateExpression(op, parameter, suppressErrors);
            case CL_GroupFilterConditionType.EpisodeWatchedDate:
                return LegacyMappings.GetWatchedDateExpression(op, parameter, suppressErrors);
            case CL_GroupFilterConditionType.AnimeType:
                return LegacyMappings.GetAnimeTypeExpression(op, parameter, suppressErrors);
            case CL_GroupFilterConditionType.VideoQuality:
                return LegacyMappings.GetVideoQualityExpression(op, parameter, suppressErrors);
            case CL_GroupFilterConditionType.AudioLanguage:
                return LegacyMappings.GetAudioLanguageExpression(op, parameter, suppressErrors);
            case CL_GroupFilterConditionType.SubtitleLanguage:
                return LegacyMappings.GetSubtitleLanguageExpression(op, parameter, suppressErrors);
            case CL_GroupFilterConditionType.AnimeGroup:
                return LegacyMappings.GetGroupExpression(op, parameter, suppressErrors);
            case CL_GroupFilterConditionType.AniDBRating:
                return LegacyMappings.GetRatingExpression(op, parameter, suppressErrors);
            case CL_GroupFilterConditionType.UserRating:
                return LegacyMappings.GetUserRatingExpression(op, parameter, suppressErrors);
            case CL_GroupFilterConditionType.AssignedMALInfo:
                return suppressErrors ? null : throw new NotSupportedException("MAL is Deprecated");
            case CL_GroupFilterConditionType.EpisodeCount:
                return LegacyMappings.GetEpisodeCountExpression(op, parameter, suppressErrors);
            case CL_GroupFilterConditionType.Year:
                return LegacyMappings.GetYearExpression(op, parameter, suppressErrors);
            case CL_GroupFilterConditionType.Season:
                return LegacyMappings.GetSeasonExpression(op, parameter, suppressErrors);
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(condition), $@"ConditionType {(CL_GroupFilterConditionType)condition.ConditionType} is not valid");
        }
    }

    public static SortingExpression GetSortingExpression(List<LegacyGroupFilterSortingCriteria> sorting)
    {
        SortingExpression expression = null;
        SortingExpression result = null;
        foreach (var criteria in sorting)
        {
            SortingExpression expr = criteria.SortType switch
            {
                CL_GroupFilterSorting.SeriesAddedDate => new AddedDateSortingSelector
                {
                    Descending = criteria.SortDirection == CL_GroupFilterSortDirection.Desc
                },
                CL_GroupFilterSorting.EpisodeAddedDate => new LastAddedDateSortingSelector
                {
                    Descending = criteria.SortDirection == CL_GroupFilterSortDirection.Desc
                },
                CL_GroupFilterSorting.EpisodeAirDate => new LastAirDateSortingSelector
                {
                    Descending = criteria.SortDirection == CL_GroupFilterSortDirection.Desc
                },
                CL_GroupFilterSorting.EpisodeWatchedDate => new LastWatchedDateSortingSelector
                {
                    Descending = criteria.SortDirection == CL_GroupFilterSortDirection.Desc
                },
                CL_GroupFilterSorting.GroupName => new NameSortingSelector
                {
                    Descending = criteria.SortDirection == CL_GroupFilterSortDirection.Desc
                },
                CL_GroupFilterSorting.Year => new AirDateSortingSelector
                {
                    Descending = criteria.SortDirection == CL_GroupFilterSortDirection.Desc
                },
                CL_GroupFilterSorting.SeriesCount => new SeriesCountSortingSelector
                {
                    Descending = criteria.SortDirection == CL_GroupFilterSortDirection.Desc
                },
                CL_GroupFilterSorting.UnwatchedEpisodeCount => new UnwatchedEpisodeCountSortingSelector
                {
                    Descending = criteria.SortDirection == CL_GroupFilterSortDirection.Desc
                },
                CL_GroupFilterSorting.MissingEpisodeCount => new MissingEpisodeCountSortingSelector
                {
                    Descending = criteria.SortDirection == CL_GroupFilterSortDirection.Desc
                },
                CL_GroupFilterSorting.UserRating => new HighestUserRatingSortingSelector
                {
                    Descending = criteria.SortDirection == CL_GroupFilterSortDirection.Desc
                },
                CL_GroupFilterSorting.AniDBRating => new AverageAniDBRatingSortingSelector
                {
                    Descending = criteria.SortDirection == CL_GroupFilterSortDirection.Desc
                },
                CL_GroupFilterSorting.SortName => new SortingNameSortingSelector
                {
                    Descending = criteria.SortDirection == CL_GroupFilterSortDirection.Desc
                },
                CL_GroupFilterSorting.GroupFilterName => new NameSortingSelector
                {
                    Descending = criteria.SortDirection == CL_GroupFilterSortDirection.Desc
                },
                _ => new NameSortingSelector
                {
                    Descending = criteria.SortDirection == CL_GroupFilterSortDirection.Desc
                }
            };
            if (expression == null) result = expression = expr;
            else expression.Next = expr;
            expression = expression.Next;
        }

        return result ?? new NameSortingSelector();
    }

    public static SortingExpression GetSortingExpression(string sorting)
    {
        if (string.IsNullOrEmpty(sorting)) return new NameSortingSelector();
        var sortCriteriaList = new List<LegacyGroupFilterSortingCriteria>();
        foreach (var pair in sorting.Split('|').Select(a => a.Split(';')))
        {
            if (pair.Length != 2)
                continue;

            if (int.TryParse(pair[0], out var filterSorting) && filterSorting > 0 && int.TryParse(pair[1], out var sortDirection) && sortDirection > 0)
            {
                var criteria = new LegacyGroupFilterSortingCriteria
                {
                    GroupFilterID = 0,
                    SortType = (CL_GroupFilterSorting)filterSorting,
                    SortDirection = (CL_GroupFilterSortDirection)sortDirection
                };
                sortCriteriaList.Add(criteria);
            }
        }

        return GetSortingExpression(sortCriteriaList);
    }
}
