using System;

namespace Shoko.Commons.Utils
{
    public static class AniDB
    {
        public static string GetAniDBDate(int secs)
        {
            if (secs == 0) return "";
            var thisDate = new DateTime(1970, 1, 1, 0, 0, 0);
            thisDate = thisDate.AddSeconds(secs);
            return thisDate.ToString("dd MMM yyyy");
        }

        public static DateTime? GetAniDBDateAsDate(int secs)
        {
            if (secs == 0) return null;
            var thisDate = new DateTime(1970, 1, 1, 0, 0, 0);
            thisDate = thisDate.AddSeconds(secs);
            return thisDate;
        }

        public static int GetAniDBDateAsSeconds(DateTime? dtDate)
        {
            if (dtDate == null) return 0;
            var startDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var ts = dtDate.Value - startDate;
            return (int)ts.TotalSeconds;
        }
    }
}
