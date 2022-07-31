
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
            return enumerable.OrderBy(OrderByAirDate);
        }

        private static DateTime? OrderByAirDate(SVR_AnimeSeries series)
        {
            var airdate = series.GetAnime()?.AirDate;
            if (airdate.HasValue && airdate.Value != DateTime.MinValue)
                return airdate;

            return null;
        }

        public static IOrderedEnumerable<SVR_AnimeGroup> OrderByName(this IEnumerable<SVR_AnimeGroup> enumerable)
        {
            return enumerable.OrderBy(OrderByName);
        }

        public static IOrderedEnumerable<SVR_GroupFilter> OrderByName(this IEnumerable<SVR_GroupFilter> enumerable)
        {
            return enumerable.OrderBy(OrderByName);
        }

        private static string OrderByName(SVR_AnimeGroup group)
        {
            return group.SortName;
        }

        private static string OrderByName(SVR_GroupFilter group)
        {
            return group.GroupFilterName;
        }
    }
}