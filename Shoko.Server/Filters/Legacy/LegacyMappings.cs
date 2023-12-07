using System;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Server.Filters.Files;
using Shoko.Server.Filters.Functions;
using Shoko.Server.Filters.Info;
using Shoko.Server.Filters.Logic.DateTimes;
using Shoko.Server.Filters.Logic.Expressions;
using Shoko.Server.Filters.Logic.Numbers;
using Shoko.Server.Filters.Selectors.DateSelectors;
using Shoko.Server.Filters.Selectors.NumberSelectors;
using Shoko.Server.Filters.User;
using Shoko.Server.Repositories;

namespace Shoko.Server.Filters.Legacy;

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

    public static AndExpression GetCompletedExpression()
    {
        return new AndExpression(
            new AndExpression(new NotExpression(new HasUnwatchedEpisodesExpression()), new NotExpression(new HasMissingEpisodesExpression())),
            new IsFinishedExpression());
    }

    public static FilterExpression<bool> GetTagExpression(GroupFilterOperator op, string parameter, bool suppressErrors=false)
    {
        if (string.IsNullOrEmpty(parameter)) return suppressErrors ? null : throw new ArgumentNullException(nameof(parameter));
        var tags = parameter.Split(new[]
        {
            '|', ','
        }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tags.Length == 0) return suppressErrors ? null : throw new ArgumentException(@$"Parameter {parameter} was invalid", nameof(parameter));
        switch (op)
        {
            case GroupFilterOperator.Include:
            case GroupFilterOperator.In:
                {
                    if (tags.Length == 1) return new HasTagExpression(tags[0]);

                    FilterExpression<bool> first = new HasTagExpression(tags[0]);
                    return tags.Skip(1).Aggregate(first, (a, b) => new OrExpression(a, new HasTagExpression(b)));
                }
            case GroupFilterOperator.Exclude:
            case GroupFilterOperator.NotIn:
                {
                    if (tags.Length == 1) return new NotExpression(new HasTagExpression(tags[0]));

                    FilterExpression<bool> first = new HasTagExpression(tags[0]);
                    return new NotExpression(tags.Skip(1).Aggregate(first, (a, b) => new OrExpression(a, new HasTagExpression(b))));
                }
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op),
                    $@"ConditionOperator {op} not applicable for Tags");
        }
    }

    public static FilterExpression<bool> GetCustomTagExpression(GroupFilterOperator op, string parameter, bool suppressErrors=false)
    {
        if (string.IsNullOrEmpty(parameter)) return suppressErrors ? null : throw new ArgumentNullException(nameof(parameter));
        var tags = parameter.Split(new[]
        {
            '|', ','
        }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        switch (op)
        {
            case GroupFilterOperator.Include:
            case GroupFilterOperator.In:
                {
                    if (tags.Length <= 1) return new HasCustomTagExpression(tags[0]);

                    FilterExpression<bool> first = new HasCustomTagExpression(tags[0]);
                    return tags.Skip(1).Aggregate(first, (a, b) => new OrExpression(a, new HasCustomTagExpression(b)));
                }
            case GroupFilterOperator.Exclude:
            case GroupFilterOperator.NotIn:
                {
                    if (tags.Length <= 1) return new NotExpression(new HasCustomTagExpression(tags[0]));

                    FilterExpression<bool> first = new HasCustomTagExpression(tags[0]);
                    return new NotExpression(tags.Skip(1).Aggregate(first, (a, b) => new OrExpression(a, new HasCustomTagExpression(b))));
                }
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
        if (string.IsNullOrEmpty(parameter)) return suppressErrors ? null : throw new ArgumentNullException(nameof(parameter));
        var tags = parameter.Split(new[]
        {
            '|', ','
        }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        switch (op)
        {
            case GroupFilterOperator.Include:
            case GroupFilterOperator.In:
                {
                    if (tags.Length <= 1) return new HasVideoSourceExpression(tags[0]);

                    FilterExpression<bool> first = new HasVideoSourceExpression(tags[0]);
                    return tags.Skip(1).Aggregate(first, (a, b) => new OrExpression(a, new HasVideoSourceExpression(b)));
                }
            case GroupFilterOperator.Exclude:
            case GroupFilterOperator.NotIn:
                {
                    if (tags.Length <= 1) return new NotExpression(new HasVideoSourceExpression(tags[0]));

                    FilterExpression<bool> first = new HasVideoSourceExpression(tags[0]);
                    return new NotExpression(tags.Skip(1).Aggregate(first, (a, b) => new OrExpression(a, new HasVideoSourceExpression(b))));
                }
            case GroupFilterOperator.InAllEpisodes:
                {
                    if (tags.Length <= 1) return new HasSharedVideoSourceExpression(tags[0]);

                    FilterExpression<bool> first = new HasSharedVideoSourceExpression(tags[0]);
                    return tags.Skip(1).Aggregate(first, (a, b) => new OrExpression(a, new HasSharedVideoSourceExpression(b)));
                }
            case GroupFilterOperator.NotInAllEpisodes:
                {
                    if (tags.Length <= 1) return new NotExpression(new HasSharedVideoSourceExpression(tags[0]));

                    FilterExpression<bool> first = new HasSharedVideoSourceExpression(tags[0]);
                    return new NotExpression(tags.Skip(1).Aggregate(first, (a, b) => new OrExpression(a, new HasSharedVideoSourceExpression(b))));
                }
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Video Quality");
        }
    }

    public static FilterExpression<bool> GetAudioLanguageExpression(GroupFilterOperator op, string parameter, bool suppressErrors = false)
    {
        if (string.IsNullOrEmpty(parameter)) return suppressErrors ? null : throw new ArgumentNullException(nameof(parameter));
        var tags = parameter.Split(new[]
        {
            '|', ','
        }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        switch (op)
        {
            case GroupFilterOperator.In:
            case GroupFilterOperator.Include:
                {
                    if (tags.Length <= 1) return new HasAudioLanguageExpression(tags[0]);

                    FilterExpression<bool> first = new HasAudioLanguageExpression(tags[0]);
                    return tags.Skip(1).Aggregate(first, (a, b) => new OrExpression(a, new HasAudioLanguageExpression(b)));
                }
            case GroupFilterOperator.NotIn:
            case GroupFilterOperator.Exclude:
                {
                    if (tags.Length <= 1) return new NotExpression(new HasAudioLanguageExpression(tags[0]));

                    FilterExpression<bool> first = new HasAudioLanguageExpression(tags[0]);
                    return new NotExpression(tags.Skip(1).Aggregate(first, (a, b) => new OrExpression(a, new HasAudioLanguageExpression(b))));
                }
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Audio Languages");
        }
    }
    
    public static FilterExpression<bool> GetSubtitleLanguageExpression(GroupFilterOperator op, string parameter, bool suppressErrors = false)
    {
        if (string.IsNullOrEmpty(parameter)) return suppressErrors ? null : throw new ArgumentNullException(nameof(parameter));
        var tags = parameter.Split(new[]
        {
            '|', ','
        }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        switch (op)
        {
            case GroupFilterOperator.In:
            case GroupFilterOperator.Include:
                {
                    if (tags.Length <= 1) return new HasSubtitleLanguageExpression(tags[0]);

                    FilterExpression<bool> first = new HasSubtitleLanguageExpression(tags[0]);
                    return tags.Skip(1).Aggregate(first, (a, b) => new OrExpression(a, new HasSubtitleLanguageExpression(b)));
                }
            case GroupFilterOperator.NotIn:
            case GroupFilterOperator.Exclude:
                {
                    if (tags.Length <= 1) return new NotExpression(new HasSubtitleLanguageExpression(tags[0]));

                    FilterExpression<bool> first = new HasSubtitleLanguageExpression(tags[0]);
                    return new NotExpression(tags.Skip(1).Aggregate(first, (a, b) => new OrExpression(a, new HasSubtitleLanguageExpression(b))));
                }
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Subtitle Languages");
        }
    }

    public static FilterExpression<bool> GetAnimeTypeExpression(GroupFilterOperator op, string parameter, bool suppressErrors = false)
    {
        if (string.IsNullOrEmpty(parameter)) return suppressErrors ? null : throw new ArgumentNullException(nameof(parameter));
        var tags = parameter.Split(new[]
        {
            '|', ','
        }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        switch (op)
        {
            case GroupFilterOperator.In:
            case GroupFilterOperator.Include:
                {
                    if (tags.Length <= 1) return new HasAnimeTypeExpression(tags[0].Replace(" ", ""));

                    FilterExpression<bool> first = new HasAnimeTypeExpression(tags[0].Replace(" ", ""));
                    return tags.Skip(1).Aggregate(first, (a, b) => new OrExpression(a, new HasAnimeTypeExpression(b.Replace(" ", ""))));
                }
            case GroupFilterOperator.NotIn:
            case GroupFilterOperator.Exclude:
                {
                    if (tags.Length <= 1) return new NotExpression(new HasAnimeTypeExpression(tags[0].Replace(" ", "")));

                    FilterExpression<bool> first = new HasAnimeTypeExpression(tags[0].Replace(" ", ""));
                    return new NotExpression(tags.Skip(1).Aggregate(first, (a, b) => new OrExpression(a, new HasAnimeTypeExpression(b.Replace(" ", "")))));
                }
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Anime Type");
        }
    }

    public static FilterExpression<bool> GetGroupExpression(GroupFilterOperator op, string parameter, bool suppressErrors = false)
    {
        if (string.IsNullOrEmpty(parameter)) return suppressErrors ? null : throw new ArgumentNullException(nameof(parameter));
        var groups = parameter.Split(new[]
        {
            '|', ','
        }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(a =>
        {
            if (!int.TryParse(a, out var groupID))
            {
                if (RepoFactory.AnimeGroup.GetAll().Any(b => b.GroupName.Equals(a, StringComparison.InvariantCultureIgnoreCase))) return a;
                throw new ArgumentOutOfRangeException(nameof(op), $@"ID {a} not found for Group");
            }
            var group = RepoFactory.AnimeGroup.GetByID(groupID);
            if (group == null) throw new ArgumentOutOfRangeException(nameof(op), $@"ID {a} not found for Group");
            return group.GroupName;
        }).ToArray();
        switch (op)
        {
            case GroupFilterOperator.In:
            case GroupFilterOperator.Include:
                {
                    if (groups.Length <= 1) return new HasNameExpression(groups[0]);

                    FilterExpression<bool> first = new HasNameExpression(groups[0]);
                    return groups.Skip(1).Aggregate(first, (a, b) => new OrExpression(a, new HasNameExpression(b)));
                }
            case GroupFilterOperator.NotIn:
            case GroupFilterOperator.Exclude:
                {
                    if (groups.Length <= 1) return new NotExpression(new HasNameExpression(groups[0]));

                    FilterExpression<bool> first = new HasNameExpression(groups[0]);
                    return new NotExpression(groups.Skip(1).Aggregate(first, (a, b) => new OrExpression(a, new HasNameExpression(b))));
                }
            case GroupFilterOperator.Equals:
                {
                    if (groups.Length > 1) throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Group Name and more than one value");
                    return new HasNameExpression(groups[0]);
                }
            case GroupFilterOperator.NotEquals:
                {
                    if (groups.Length > 1) throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Group Name and more than one value");

                    if (!int.TryParse(groups[0], out var groupID)) throw new ArgumentOutOfRangeException(nameof(op), $@"ID {groups[0]} not found for Group");
                    var group = RepoFactory.AnimeGroup.GetByID(groupID);
                    if (group == null) throw new ArgumentOutOfRangeException(nameof(op), $@"ID {groups[0]} not found for Group");

                    return new NotExpression(new HasNameExpression(groups[0]));
                }
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
                return new NumberLessThanExpression(new AverageAniDBRatingSelector(), rating);
            case GroupFilterOperator.LessThan:
                return new NumberGreaterThanExpression(new AverageAniDBRatingSelector(), rating);
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
            case GroupFilterOperator.GreaterThan:
                return new NumberLessThanExpression(new EpisodeCountSelector(), count);
            case GroupFilterOperator.LessThan:
                return new NumberGreaterThanExpression(new EpisodeCountSelector(), count);
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Episode Count");
        }
    }

    public static FilterExpression<bool> GetYearExpression(GroupFilterOperator op, string parameter, bool suppressErrors = false)
    {
        if (string.IsNullOrEmpty(parameter)) return suppressErrors ? null : throw new ArgumentNullException(nameof(parameter));
        var tags = parameter.Split(new[]
        {
            '|', ','
        }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        switch (op)
        {
            case GroupFilterOperator.In:
            case GroupFilterOperator.Include:
                {
                    if (!int.TryParse(tags[0], out var firstYear))
                        return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} is not a number", nameof(parameter));
                    if (tags.Length <= 1) return new InYearExpression(firstYear);

                    FilterExpression<bool> first = new InYearExpression(firstYear);
                    return tags.Skip(1).Aggregate(first, (a, b) =>
                    {
                        if (!int.TryParse(b, out var year))
                            return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} is not a number", nameof(parameter));
                        return new OrExpression(a, new InYearExpression(year));
                    });
                }
            case GroupFilterOperator.NotIn:
            case GroupFilterOperator.Exclude:
                {
                    if (!int.TryParse(tags[0], out var firstYear))
                        return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} is not a number", nameof(parameter));
                    if (tags.Length <= 1) return new NotExpression(new InYearExpression(firstYear));

                    FilterExpression<bool> first = new InYearExpression(firstYear);
                    return new NotExpression(tags.Skip(1).Aggregate(first, (a, b) =>
                    {
                        if (!int.TryParse(b, out var year))
                            return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} is not a number", nameof(parameter));
                        return new OrExpression(a, new InYearExpression(year));
                    }));
                }
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Years");
        }
    }

    public static FilterExpression<bool> GetSeasonExpression(GroupFilterOperator op, string parameter, bool suppressErrors = false)
    {
        if (string.IsNullOrEmpty(parameter)) return suppressErrors ? null : throw new ArgumentNullException(nameof(parameter));
        var tags = parameter.Split(new[]
        {
            '|', ','
        }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        switch (op)
        {
            case GroupFilterOperator.Include:
            case GroupFilterOperator.In:
                {
                    var firstParts = tags[0].Split(' ');
                    if (!int.TryParse(firstParts[1], out var firstYear))
                        return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} does not have a valid year", nameof(parameter));
                    if (!Enum.TryParse<AnimeSeason>(firstParts[0], out var firstSeason))
                        return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} does not have a valid season", nameof(parameter));
                    if (tags.Length <= 1) return new InSeasonExpression(firstYear, firstSeason);

                    FilterExpression<bool> first = new InYearExpression(firstYear);
                    return tags.Skip(1).Aggregate(first, (a, b) =>
                    {
                        var parts = b.Split(' ');
                        if (!int.TryParse(parts[1], out var year))
                            return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} does not have a valid year", nameof(parameter));
                        if (!Enum.TryParse<AnimeSeason>(parts[0], out var season))
                            return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} does not have a valid season", nameof(parameter));
                        return new OrExpression(a, new InSeasonExpression(year, season));
                    });
                }
            case GroupFilterOperator.Exclude:
            case GroupFilterOperator.NotIn:
                {
                    var firstParts = tags[0].Split(' ');
                    if (!int.TryParse(firstParts[1], out var firstYear))
                        return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} does not have a valid year", nameof(parameter));
                    if (!Enum.TryParse<AnimeSeason>(firstParts[0], out var firstSeason))
                        return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} does not have a valid season", nameof(parameter));
                    if (tags.Length <= 1) return new NotExpression(new InSeasonExpression(firstYear, firstSeason));

                    FilterExpression<bool> first = new InYearExpression(firstYear);
                    return new NotExpression(tags.Skip(1).Aggregate(first, (a, b) =>
                    {
                        var parts = b.Split(' ');
                        if (!int.TryParse(parts[1], out var year))
                            return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} does not have a valid year", nameof(parameter));
                        if (!Enum.TryParse<AnimeSeason>(parts[0], out var season))
                            return suppressErrors ? null : throw new ArgumentException($@"Parameter {parameter} does not have a valid season", nameof(parameter));
                        return new OrExpression(a, new InSeasonExpression(year, season));
                    }));
                }
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(op), $@"ConditionOperator {op} not applicable for Seasons");
        }
    }
}
