
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
    }
}