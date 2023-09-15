using System;
using Shoko.Models.Enums;
using Shoko.Server.Filters.Files;
using Shoko.Server.Filters.Functions;
using Shoko.Server.Filters.Info;
using Shoko.Server.Filters.Logic;
using Shoko.Server.Filters.Logic.Numbers;
using Shoko.Server.Filters.Logic.DateTimes;
using Shoko.Server.Filters.Selectors;

namespace Shoko.Server.Filters;

public class LegacyMappings
{
    public static bool TryParseDate(string sDate, out DateTime result)
    {
        // yyyyMMdd or yyyy-MM-dd
        result = DateTime.Today;
        if (sDate.Length != 8 && sDate.Length != 10) return false;
        if (!int.TryParse(sDate[..4], out var year)) return false;
        int month;
        int day;
        if (sDate.Length == 8)
        {
            if (!int.TryParse(sDate[4..6], out month)) return false;
            if (!int.TryParse(sDate[6..8], out day)) return false;
        }
        else
        {
            if (!int.TryParse(sDate[5..7], out month)) return false;
            if (!int.TryParse(sDate[8..10], out day)) return false;
        }

        result =  new DateTime(year, month, day);
        return true;
    }

    public static FilterExpression<bool> GetTagExpression(GroupFilterOperator op, string parameter, bool suppressErrors=false)
    {
        if (string.IsNullOrEmpty(parameter)) return suppressErrors ? null : throw new ArgumentNullException(nameof(parameter));
        switch (op)
        {
            case GroupFilterOperator.Include:
            case GroupFilterOperator.In:
                return new HasTagExpression(parameter);
            case GroupFilterOperator.Exclude:
            case GroupFilterOperator.NotIn:
                return new NotExpression(new HasTagExpression(parameter));
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op),
                    $@"ConditionOperator {op} not applicable for Tags");
        }
    }

    public static FilterExpression<bool> GetCustomTagExpression(GroupFilterOperator op, string parameter, bool suppressErrors=false)
    {
        if (string.IsNullOrEmpty(parameter)) return suppressErrors ? null : throw new ArgumentNullException(nameof(parameter));
        switch (op)
        {
            case GroupFilterOperator.Include:
            case GroupFilterOperator.In:
                return new HasCustomTagExpression(parameter);
            case GroupFilterOperator.Exclude:
            case GroupFilterOperator.NotIn:
                return new NotExpression(new HasCustomTagExpression(parameter));
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op),
                    $@"ConditionOperator {op} not applicable for Tags");
        }
    }

    public static FilterExpression<bool> GetAirDateExpression(GroupFilterOperator op, string parameter, bool suppressErrors=false)
    {
        return GetDateExpression(new AirDateSelector(), op, parameter, suppressErrors);
    }
    
    public static FilterExpression<bool> GetLastAirDateExpression(GroupFilterOperator op, string parameter, bool suppressErrors=false)
    {
        return GetDateExpression(new LastAirDateSelector(), op, parameter, suppressErrors);
    }

    public static FilterExpression<bool> GetAddedDateExpression(GroupFilterOperator op, string parameter, bool suppressErrors=false)
    {
        return GetDateExpression(new AddedDateSelector(), op, parameter, suppressErrors);
    }

    public static FilterExpression<bool> GetLastAddedDateExpression(GroupFilterOperator op, string parameter, bool suppressErrors=false)
    {
        return GetDateExpression(new LastAddedDateSelector(), op, parameter, suppressErrors);
    }

    public static FilterExpression<bool> GetWatchedDateExpression(GroupFilterOperator op, string parameter, bool suppressErrors=false)
    {
        return GetDateExpression(new LastWatchedDateSelector(), op, parameter, suppressErrors);
    }

    private static FilterExpression<bool> GetDateExpression(FilterExpression<DateTime?> selector, GroupFilterOperator op, string parameter, bool suppressErrors)
    {
        if (string.IsNullOrEmpty(parameter)) return suppressErrors ? null : throw new ArgumentNullException(nameof(parameter));
        switch (op)
        {
            case GroupFilterOperator.LastXDays:
                {
                    if (!int.TryParse(parameter, out var lastX))
                        return suppressErrors ? null : throw new ArgumentException(@"Parameter is not a number", nameof(parameter));
                    return new DateGreaterThanEqualsExpression(selector,
                        new DateDiffFunction(new DateAddFunction(new TodayFunction(), TimeSpan.FromDays(1) - TimeSpan.FromMilliseconds(1)),
                            TimeSpan.FromDays(lastX)));
                }
            case GroupFilterOperator.GreaterThan:
                {
                    if (!TryParseDate(parameter, out var date))
                        return suppressErrors
                            ? null
                            : throw new ArgumentException($@"Parameter {parameter} was not a date in format of yyyyMMdd", nameof(parameter));
                    return new DateGreaterThanExpression(selector, date);
                }
            case GroupFilterOperator.LessThan:
                {
                    if (!TryParseDate(parameter, out var date))
                        return suppressErrors
                            ? null
                            : throw new ArgumentException($@"Parameter {parameter} was not a date in format of yyyyMMdd", nameof(parameter));
                    return new DateGreaterThanExpression(selector, date);
                }
            default:
                return suppressErrors
                    ? null
                    : throw new ArgumentOutOfRangeException(nameof(op),
                        $@"ConditionOperator {op} not applicable for Date filters");
        }
    }

    public static FilterExpression<bool> GetVideoQualityExpression(GroupFilterOperator op, string parameter, bool suppressErrors = false)
    {
        switch (op)
        {
            case GroupFilterOperator.In:
                return new HasVideoSourceExpression(parameter);
            case GroupFilterOperator.InAllEpisodes:
                return new HasSharedVideoSourceExpression(parameter);
            case GroupFilterOperator.NotIn:
                return new NotExpression(new HasVideoSourceExpression(parameter));
            case GroupFilterOperator.NotInAllEpisodes:
                return new NotExpression(new HasSharedVideoSourceExpression(parameter));
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Video Quality");
        }
    }
    
    public static FilterExpression<bool> GetAudioLanguageExpression(GroupFilterOperator op, string parameter, bool suppressErrors = false)
    {
        switch (op)
        {
            case GroupFilterOperator.In:
                return new HasAudioLanguageExpression(parameter);
            case GroupFilterOperator.NotIn:
                return new NotExpression(new HasAudioLanguageExpression(parameter));
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Audio Languages");
        }
    }
    
    public static FilterExpression<bool> GetSubtitleLanguageExpression(GroupFilterOperator op, string parameter, bool suppressErrors = false)
    {
        switch (op)
        {
            case GroupFilterOperator.In:
                return new HasSubtitleLanguageExpression(parameter);
            case GroupFilterOperator.NotIn:
                return new NotExpression(new HasSubtitleLanguageExpression(parameter));
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Subtitle Languages");
        }
    }

    public static FilterExpression<bool> GetAnimeTypeExpression(GroupFilterOperator op, string parameter, bool suppressErrors = false)
    {
        switch (op)
        {
            case GroupFilterOperator.In:
                return new HasAnimeTypeExpression(parameter);
            case GroupFilterOperator.NotIn:
                return new NotExpression(new HasAnimeTypeExpression(parameter));
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Anime Type");
        }
    }

    public static FilterExpression<bool> GetGroupExpression(GroupFilterOperator op, string parameter, bool suppressErrors = false)
    {
        switch (op)
        {
            case GroupFilterOperator.In:
                return new HasNameExpression(parameter);
            case GroupFilterOperator.NotIn:
                return new NotExpression(new HasNameExpression(parameter));
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Group Name");
        }
    }

    public static FilterExpression<bool> GetRatingExpression(GroupFilterOperator op, string parameter, bool suppressErrors = false)
    {
        if (!double.TryParse(parameter, out var rating))
            return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} is not a number", nameof(parameter));
        switch (op)
        {
            // These are reversed because we would consider that parameter is greater than the rating, but the expression takes a constant as the second operand
            case GroupFilterOperator.GreaterThan:
                return new NumberLessThanExpression(new HighestAniDBRatingSelector(), rating);
            case GroupFilterOperator.LessThan:
                return new NumberGreaterThanExpression(new HighestAniDBRatingSelector(), rating);
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Rating");
        }
    }

    public static FilterExpression<bool> GetUserRatingExpression(GroupFilterOperator op, string parameter, bool suppressErrors = false)
    {
        if (!double.TryParse(parameter, out var rating))
            return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} is not a number", nameof(parameter));
        switch (op)
        {
            // These are reversed because we would consider that parameter is greater than the rating, but the expression takes a constant as the second operand
            case GroupFilterOperator.GreaterThan:
                return new NumberLessThanExpression(new HighestUserRatingSelector(), rating);
            case GroupFilterOperator.LessThan:
                return new NumberGreaterThanExpression(new HighestUserRatingSelector(), rating);
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for User Rating");
        }
    }

    public static FilterExpression<bool> GetEpisodeCountExpression(GroupFilterOperator op, string parameter, bool suppressErrors = false)
    {
        if (!int.TryParse(parameter, out var count))
            return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} is not a number", nameof(parameter));
        switch (op)
        {
            // These are reversed because we would consider that parameter is greater than the rating, but the expression takes a constant as the second operand
            case GroupFilterOperator.GreaterThan:
                return new NumberLessThanExpression(new EpisodeCountSelector(), count);
            case GroupFilterOperator.LessThan:
                return new NumberGreaterThanExpression(new HighestUserRatingSelector(), count);
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Episode Count");
        }
    }

    public static FilterExpression<bool> GetYearExpression(GroupFilterOperator op, string parameter, bool suppressErrors = false)
    {
        if (!int.TryParse(parameter, out var year))
            return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} is not a number", nameof(parameter));
        switch (op)
        {
            case GroupFilterOperator.In:
            case GroupFilterOperator.Include:
                return new InYearExpression(year);
            case GroupFilterOperator.NotIn:
            case GroupFilterOperator.Exclude:
                return new NotExpression(new InYearExpression(year));
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Years");
        }
    }

    public static FilterExpression<bool> GetSeasonExpression(GroupFilterOperator op, string parameter, bool suppressErrors = false)
    {
        var parts = parameter.Split(' ');
        if (!int.TryParse(parts[1], out var year))
            return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} does not have a valid year", nameof(parameter));
        if (!Enum.TryParse<AnimeSeason>(parts[0], out var season))
            return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} does not have a valid season", nameof(parameter));
        switch (op)
        {
            case GroupFilterOperator.In:
            case GroupFilterOperator.Include:
                return new InSeasonExpression(year, season);
            case GroupFilterOperator.NotIn:
            case GroupFilterOperator.Exclude:
                return new NotExpression(new InSeasonExpression(year, season));
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Seasons");
        }
    }
}
