using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Filters.Info;
using Shoko.Server.Filters.Logic;
using Shoko.Server.Filters.User;
using Shoko.Server.Models;

namespace Shoko.Server.Filters;

public class LegacyFilterConverter
{
    public List<GroupFilterCondition> GetConditions(Filter filter)
    {
        // TODO traverse the tree and replace with pre-set mappings
        return new List<GroupFilterCondition>();
    }

    public List<GroupFilterSortingCriteria> GetSortingCriteria(Filter filter)
    {
        // TODO traverse the tree and replace with pre-set mappings
        return new List<GroupFilterSortingCriteria>
        {
            new()
            {
                GroupFilterID = filter.FilterID, SortType = GroupFilterSorting.SortName, SortDirection = GroupFilterSortDirection.Asc
            }
        };
    }

    public FilterExpression<bool> GetExpression(List<GroupFilterCondition> conditions, bool suppressErrors = false)
    {
        // forward compatibility is easier. Just map the old conditions to an expression
        if (conditions == null || conditions.Count < 1) return null;
        var first = conditions.Select((a, index) => new {Expression=GetExpression(a, suppressErrors), Index=index}).FirstOrDefault(a => a.Expression != null);
        if (first == null) return null;
        return conditions.Count == 1 ? first.Expression : conditions.Skip(first.Index + 1).Aggregate(first.Expression, (a, b) =>
        {
            var result = GetExpression(b, suppressErrors);
            return result == null ? a : new AndExpression(a, result);
        });
    }

    private FilterExpression<bool> GetExpression(GroupFilterCondition condition, bool suppressErrors = false)
    {
        var op = (GroupFilterOperator)condition.ConditionOperator;
        var parameter = condition.ConditionParameter;
        switch ((GroupFilterConditionType)condition.ConditionType)
        {
            case GroupFilterConditionType.CompletedSeries:
                return new AndExpression(
                    new AndExpression(new NotExpression(new HasUnwatchedEpisodesExpression()), new NotExpression(new HasMissingEpisodesExpression())),
                    new IsFinishedExpression());
            case GroupFilterConditionType.MissingEpisodes:
                return new HasMissingEpisodesExpression();
            case GroupFilterConditionType.MissingEpisodesCollecting:
                return new HasMissingEpisodesCollectingExpression();
            case GroupFilterConditionType.HasUnwatchedEpisodes:
                return new HasUnwatchedEpisodesExpression();
            case GroupFilterConditionType.HasWatchedEpisodes:
                return new HasWatchedEpisodesExpression();
            case GroupFilterConditionType.UserVoted:
                return new HasPermanentUserVotesExpression();
            case GroupFilterConditionType.UserVotedAny:
                return new HasUserVotesExpression();
            case GroupFilterConditionType.Tag:
                return LegacyMappings.GetTagExpression(op, parameter, suppressErrors);
            case GroupFilterConditionType.CustomTags:
                return LegacyMappings.GetCustomTagExpression(op, parameter, suppressErrors);
            case GroupFilterConditionType.AirDate:
                return LegacyMappings.GetAirDateExpression(op, parameter, suppressErrors);
            case GroupFilterConditionType.LatestEpisodeAirDate:
                return LegacyMappings.GetLastAirDateExpression(op, parameter, suppressErrors);
            case GroupFilterConditionType.SeriesCreatedDate:
                return LegacyMappings.GetAddedDateExpression(op, parameter, suppressErrors);
            case GroupFilterConditionType.EpisodeAddedDate:
                return LegacyMappings.GetLastAddedDateExpression(op, parameter, suppressErrors);
            case GroupFilterConditionType.EpisodeWatchedDate:
                return LegacyMappings.GetWatchedDateExpression(op, parameter, suppressErrors);
            case GroupFilterConditionType.AssignedTvDBInfo:
                return new HasTvDBLinkExpression();
            case GroupFilterConditionType.AnimeType:
                return LegacyMappings.GetAnimeTypeExpression(op, parameter, suppressErrors);
            case GroupFilterConditionType.VideoQuality:
                return LegacyMappings.GetVideoQualityExpression(op, parameter, suppressErrors);
            case GroupFilterConditionType.AudioLanguage:
                return LegacyMappings.GetAudioLanguageExpression(op, parameter, suppressErrors);
            case GroupFilterConditionType.SubtitleLanguage:
                return LegacyMappings.GetSubtitleLanguageExpression(op, parameter, suppressErrors);
            case GroupFilterConditionType.Favourite:
                return new IsFavoriteExpression();
            case GroupFilterConditionType.AnimeGroup:
                return LegacyMappings.GetGroupExpression(op, parameter, suppressErrors);
            case GroupFilterConditionType.AniDBRating:
                return LegacyMappings.GetRatingExpression(op, parameter, suppressErrors);
            case GroupFilterConditionType.UserRating:
                return LegacyMappings.GetUserRatingExpression(op, parameter, suppressErrors);
            case GroupFilterConditionType.FinishedAiring:
                return new IsFinishedExpression();
            case GroupFilterConditionType.AssignedTvDBOrMovieDBInfo:
                return new OrExpression(new HasTvDBLinkExpression(), new HasTMDbLinkExpression());
            case GroupFilterConditionType.AssignedMovieDBInfo:
                return new HasTMDbLinkExpression();
            case GroupFilterConditionType.AssignedMALInfo:
                return suppressErrors ? null : throw new NotSupportedException("MAL is Deprecated");
            case GroupFilterConditionType.EpisodeCount:
                return LegacyMappings.GetEpisodeCountExpression(op, parameter, suppressErrors);
            case GroupFilterConditionType.Year:
                return LegacyMappings.GetYearExpression(op, parameter, suppressErrors);
            case GroupFilterConditionType.Season:
                return LegacyMappings.GetSeasonExpression(op, parameter, suppressErrors);
            case GroupFilterConditionType.AssignedTraktInfo:
                return new HasTraktLinkExpression();
            default:
                return suppressErrors ? null : throw new ArgumentOutOfRangeException(nameof(condition), $@"ConditionType {condition.ConditionType} is not valid");
        }
    }

}
