using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Server.Models;

namespace Shoko.Server.API.v3
{
    public static class APIGroupFilterSortingHelper
    {
        public static IEnumerable<SVR_AnimeGroup> GroupFilterSort(this IEnumerable<SVR_AnimeGroup> groups, SVR_GroupFilter gf)
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

        private static IOrderedEnumerable<SVR_AnimeGroup> Order<T>(IEnumerable<SVR_AnimeGroup> groups,
            Func<SVR_AnimeGroup, T> o,
            bool descending, bool isFirst)
        {
            if (isFirst)
                return descending 
                    ? groups.OrderByDescending(o) 
                    : groups.OrderBy(o);
            return descending
                ? ((IOrderedEnumerable<SVR_AnimeGroup>) groups).ThenByDescending(o)
                : ((IOrderedEnumerable<SVR_AnimeGroup>) groups).ThenBy(o);
        }
    }
}