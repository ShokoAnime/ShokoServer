using System;
using System.Globalization;

namespace Shoko.Commons.Utils
{
    public static class AniDB
    {
        public static string GetAniDBDate(int secs, IFormatProvider culture = null)
        {
            if (secs == 0) return "";
            if (culture == null)
                culture = CultureInfo.CurrentCulture;
            DateTime thisDate = new DateTime(1970, 1, 1, 0, 0, 0);
            thisDate = thisDate.AddSeconds(secs);
            return thisDate.ToString("dd MMM yyyy", culture);
        }

        public static DateTime? GetAniDBDateAsDate(int secs)
        {
            if (secs == 0) return null;

            DateTime thisDate = new DateTime(1970, 1, 1, 0, 0, 0);
            thisDate = thisDate.AddSeconds(secs);
            return thisDate;
        }

        public static int GetAniDBDateAsSeconds(string dateXML, bool isStartDate)
        {
            // eg "2008-12-31" or "2008-12" or "2008"
            if (dateXML == null || dateXML.Trim().Length < 4) return 0;

            string month;
            string day;

            string year = dateXML.Trim().Substring(0, 4);

            if (dateXML.Trim().Length > 4)
                month = dateXML.Trim().Substring(5, 2);
            else
            {
                if (isStartDate)
                    month = "1";
                else
                    month = "12";
            }

            if (dateXML.Trim().Length > 7)
                day = dateXML.Trim().Substring(8, 2);
            else
            {
                if (isStartDate)
                    day = "1";
                else
                {
                    // find the last day of the month
                    int numberOfDays = DateTime.DaysInMonth(int.Parse(year), int.Parse(month));
                    day = numberOfDays.ToString();
                }
            }

            //BaseConfig.MyAnimeLog.Write("Date = {0}/{1}/{2}", year, month, day);


            DateTime actualDate = new DateTime(int.Parse(year), int.Parse(month), int.Parse(day), 0, 0, 0);
            //startDate = startDate.AddDays(-1);

            return GetAniDBDateAsSeconds(actualDate);
        }

        public static DateTime? GetAniDBDateAsDate(string dateInSeconds, int dateFlags)
        {
            // DateFlags
            // 0 = normal start and end date (2010-01-31)
            // 1 = start date is year-month (2010-01)
            // 2 = start date is a year (2010)
            // 4 = normal start date, year-month end date
            // 8 = normal start date, year end date
            // 10 = start date is a year (2010)
            // 16 = normal start and end date (2010-01-31)

            if (!double.TryParse(dateInSeconds, out double secs)) return null;
            if (Math.Abs(secs) < 0.1) return null;

            DateTime thisDate = new DateTime(1970, 1, 1, 0, 0, 0);
            thisDate = thisDate.AddSeconds(secs);

            // reconstruct using date flags
            int year = thisDate.Year;
            int month = thisDate.Month;
            int day = thisDate.Day;

            if (dateFlags == 2 || dateFlags == 10 || dateFlags == 1)
                month = 1;

            if (dateFlags == 1)
                day = 1;

            return new DateTime(year, month, day, 0, 0, 0);
        }

        public static int GetAniDBDateAsSeconds(DateTime? dtDate)
        {
            if (dtDate == null) return 0;

            DateTime startDate = new DateTime(1970, 1, 1, 0, 0, 0);
            TimeSpan ts = dtDate.Value - startDate;

            return (int) ts.TotalSeconds;
        }

        public static string AniDBDate(DateTime date)
        {
            TimeSpan sp = date.Subtract(new DateTime(1970, 1, 1, 0, 0, 0));
            return ((long) sp.TotalSeconds).ToString();
        }
    }
}
