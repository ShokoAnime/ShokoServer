using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Server.Models;

namespace Shoko.Server.API.v3.Helpers
{
    public static class APIGroupFilterSortingHelper
    {
        public static IEnumerable<SVR_AnimeGroup> OrderByGroupFilter(this IEnumerable<SVR_AnimeGroup> groups, SVR_GroupFilter gf)
        {
            bool isFirst = true;
            IEnumerable<SVR_AnimeGroup> query = groups;
            foreach (GroupFilterSortingCriteria gfsc in gf.SortCriteriaList)
            {
                query = Order(query, gfsc, isFirst);
                isFirst = false;
            }
            return query;
        }
        public static IEnumerable<SVR_AnimeSeries> OrderByGroupFilter(this IEnumerable<SVR_AnimeSeries> series, SVR_GroupFilter gf)
        {
            bool isFirst = true;
            IEnumerable<SVR_AnimeSeries> result = series;
            foreach (GroupFilterSortingCriteria gfsc in gf.SortCriteriaList)
            {
                result = Order(result, gfsc, isFirst);
                isFirst = false;
            }
            return result;
        }

        private static IOrderedEnumerable<SVR_AnimeGroup> Order(IEnumerable<SVR_AnimeGroup> groups,
            GroupFilterSortingCriteria gfsc, bool isFirst)
        {
            bool desc = gfsc.SortDirection == GroupFilterSortDirection.Desc;
            switch (gfsc.SortType)
            {
                case GroupFilterSorting.Year:
                    return !desc
                        ? Order(groups, a => a.Contract.Stat_AirDate_Min, false, isFirst)
                        : Order(groups, a => a.Contract.Stat_AirDate_Max, true, isFirst);
                case GroupFilterSorting.AniDBRating:
                    return Order(groups, a => a.Contract.Stat_AniDBRating, desc, isFirst);
                case GroupFilterSorting.EpisodeAddedDate:
                    return Order(groups, a => a.EpisodeAddedDate, desc, isFirst);
                case GroupFilterSorting.EpisodeAirDate:
                    return Order(groups, a => a.LatestEpisodeAirDate, desc, isFirst);
                case GroupFilterSorting.EpisodeWatchedDate:
                    return Order(groups, a => a.Contract.WatchedDate, desc, isFirst);
                case GroupFilterSorting.MissingEpisodeCount:
                    return Order(groups, a => a.MissingEpisodeCount, desc, isFirst);
                case GroupFilterSorting.SeriesAddedDate:
                    return Order(groups, a => a.Contract.Stat_SeriesCreatedDate, desc, isFirst);
                case GroupFilterSorting.SeriesCount:
                    return Order(groups, a => a.Contract.Stat_SeriesCount, desc, isFirst);
                case GroupFilterSorting.SortName:
                    return Order(groups, a => a.SortName, desc, isFirst);
                case GroupFilterSorting.UnwatchedEpisodeCount:
                    return Order(groups, a => a.Contract.UnwatchedEpisodeCount, desc, isFirst);
                case GroupFilterSorting.UserRating:
                    return Order(groups, a => a.Contract.Stat_UserVoteOverall, desc, isFirst);
                case GroupFilterSorting.GroupName:
                case GroupFilterSorting.GroupFilterName:
                    return Order(groups, a => a.GroupName, desc, isFirst);
                default:
                    return Order(groups, a => a.GroupName, desc, isFirst);
            }
        }

        private static IOrderedEnumerable<SVR_AnimeSeries> Order(IEnumerable<SVR_AnimeSeries> groups,
            GroupFilterSortingCriteria gfsc, bool isFirst)
        {
            bool desc = gfsc.SortDirection == GroupFilterSortDirection.Desc;
            switch (gfsc.SortType)
            {
                case GroupFilterSorting.Year:
                    return Order(groups, a => a.Contract.AniDBAnime.AniDBAnime.AirDate, desc, isFirst);
                case GroupFilterSorting.AniDBRating:
                    return Order(groups, a => a.Contract.AniDBAnime.AniDBAnime.Rating, desc, isFirst);
                case GroupFilterSorting.EpisodeAddedDate:
                    return Order(groups, a => a.EpisodeAddedDate, desc, isFirst);
                case GroupFilterSorting.EpisodeAirDate:
                    return Order(groups, a => a.LatestEpisodeAirDate, desc, isFirst);
                case GroupFilterSorting.EpisodeWatchedDate:
                    return Order(groups, a => a.Contract.WatchedDate, desc, isFirst);
                case GroupFilterSorting.MissingEpisodeCount:
                    return Order(groups, a => a.MissingEpisodeCount, desc, isFirst);
                case GroupFilterSorting.SeriesAddedDate:
                    return Order(groups, a => a.Contract.DateTimeCreated, desc, isFirst);
                case GroupFilterSorting.SeriesCount:
                    return Order(groups, a => 1, desc, isFirst);
                case GroupFilterSorting.SortName:
                    return Order(groups, a => a.GetSeriesName(), desc, isFirst);
                case GroupFilterSorting.UnwatchedEpisodeCount:
                    return Order(groups, a => a.Contract.UnwatchedEpisodeCount, desc, isFirst);
                case GroupFilterSorting.UserRating:
                    return Order(groups, a => a.Contract.AniDBAnime.UserVote.VoteValue, desc, isFirst);
                case GroupFilterSorting.GroupName:
                case GroupFilterSorting.GroupFilterName:
                    return Order(groups, a => a.GetSeriesName(), desc, isFirst);
                default:
                    return Order(groups, a => a.GetSeriesName(), desc, isFirst);
            }
        }

        private static IOrderedEnumerable<U> Order<T, U>(IEnumerable<U> groups,
            Func<U, T> o,
            bool descending, bool isFirst)
        {
            if (isFirst)
                return descending 
                    ? groups.OrderByDescending(o) 
                    : groups.OrderBy(o);
            return descending
                ? ((IOrderedEnumerable<U>) groups).ThenByDescending(o)
                : ((IOrderedEnumerable<U>) groups).ThenBy(o);
        }
    }
}