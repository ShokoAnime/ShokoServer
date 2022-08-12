
using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Models;

namespace Shoko.Server.Extensions
{
    
    public static class IEnumerableExtensions
    {
        public static IOrderedEnumerable<SVR_AnimeSeries> OrderByAirDate(this IEnumerable<SVR_AnimeSeries> enumerable)
        {
            return enumerable.OrderBy(series => series.GetAnime()?.AirDate ?? DateTime.MaxValue);
        }

        public static IOrderedEnumerable<SVR_AnimeGroup> OrderByName(this IEnumerable<SVR_AnimeGroup> enumerable)
        {
            return enumerable.OrderBy(group => group.GroupName);
        }

        public static IOrderedEnumerable<SVR_GroupFilter> OrderByName(this IEnumerable<SVR_GroupFilter> enumerable)
        {
            return enumerable.OrderBy(filter => filter.GroupFilterName);
        }
    }
}