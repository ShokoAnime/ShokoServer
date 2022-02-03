using System;
using System.Collections.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Info
{
    public class ResponseCalendar
    {
        /// <summary>
        /// TODO Check these for posterity
        /// 0 = normal start and end date (2010-01-31)
        /// 1 = start date is year-month (2010-01)
        /// 2 = start date is a year (2010)
        /// 4 = normal start date, year-month end date
        /// 8 = normal start date, year end date
        /// 10 = start date is a year (2010)
        /// 16 = normal start and end date (2010-01-31)
        /// </summary>
        [Flags]
        public enum CalendarFlags
        {
            DateKnown = 0,
            StartMonthDayUnknown = 1,
            StartDayUnknown = 1 << 1,
            EndMonthDayUnknown = 1 << 2,
            EndDayUnknown = 1 << 3,
            Finished = 1 << 4,
            StartUnknown = 1 << 5,
            EndUnknown = 1 << 6
        }

        public class CalendarEntry
        {
            public int AnimeID { get; set; }

            public DateTime? ReleaseDate { get; set; }

            public CalendarFlags DateFlags { get; set; }
        }

        public List<CalendarEntry> Next25Anime { get; set; }
        public List<CalendarEntry> Previous25Anime { get; set; }
    }
}
