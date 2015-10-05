using System;
using System.Collections.Generic;
using System.Linq;
using Raven.Client;

namespace JMMDatabase.Extensions
{
    public static class ServerExtensions
    {

        public static DateTime FromYYYYMMDDDate(this string sDate)
        {
            try
            {
                int year = int.Parse(sDate.Substring(0, 4));
                int month = int.Parse(sDate.Substring(4, 2));
                int day = int.Parse(sDate.Substring(6, 2));
                return new DateTime(year, month, day);
            }
            catch (Exception)
            {
                return DateTime.Today;
            }
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> d)
        {
            return new HashSet<T>(d);
        }

        public static T Find<S, T>(this Dictionary<S, T> dict, S value) where T : class
        {
            if (dict.ContainsKey(value))
                return dict[value];
            return null;
        }

        public static IEnumerable<T> SelectOrDefault<S, T>(this IEnumerable<S> org, Func<S, T> func)
        {
            if (org==null)
                return new List<T>();
            return org.Select(func);
        }

        public static bool HasItems<T>(this List<T> org)
        {
            if (org != null && org.Count > 0)
                return true;
            return false;
        }
        public static List<T> GetAll<T>(this IDocumentSession session) // Bypass RavenDB 1024 items safe mode
        {
            RavenQueryStatistics stats;
            const int ElementTakeCount = 1024;
            int i = 0;
            int skipResults = 0;
            List<T> res=new List<T>();
            List<T> objs = null;
            do
            {
                objs=session.Query<T>().Statistics(out stats).Skip(i * ElementTakeCount + skipResults).Take(ElementTakeCount).ToList();
                i++;
                skipResults += stats.SkippedResults;
                res.AddRange(objs);

            } while (objs.Count==ElementTakeCount);
            return res;
        } 
    }
}
