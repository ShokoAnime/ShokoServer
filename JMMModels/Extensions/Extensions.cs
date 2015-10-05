using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMModels.Childs;

namespace JMMModels.ClientExtensions
{
    public static class Extensions
    {
        public static void CopyTo<T>(this T s, T t)
        {
            foreach (var pS in s.GetType().GetProperties())
            {
                foreach (var pT in t.GetType().GetProperties())
                {
                    if (pT.Name != pS.Name) continue;
                    (pT.GetSetMethod()).Invoke(t, new object[] { pS.GetGetMethod().Invoke(s, null) });
                }
            };
        }
        public static long ToUnixTime(this DateTime dt)
        {
            return (long)((dt - (new DateTime(1970, 1, 1))).TotalSeconds);
        }

        public static DateTime ToDateTime(this long value)
        {
            return new DateTime(1970, 1, 1).AddSeconds(value);
        }

        public static DateTime? ToDateTime(this AniDB_Date date)
        {
            return date?.Date.ToDateTime();
        }

    }
}
