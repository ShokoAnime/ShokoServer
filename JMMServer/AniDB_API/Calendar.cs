using System;
using System.Collections.Generic;
using JMMServer;

namespace AniDBAPI
{
    public class Calendar
    {
        private int animeID;

        public int AnimeID
        {
            get { return animeID; }
            set { animeID = value; }
        }

        private DateTime? releaseDate = DateTime.Now;

        public DateTime? ReleaseDate
        {
            get { return releaseDate; }
            set { releaseDate = value; }
        }

        private string releaseDateRaw = "";

        public string ReleaseDateRaw
        {
            get { return releaseDateRaw; }
            set { releaseDateRaw = value; }
        }

        private int dateFlags = 0;

        public int DateFlags
        {
            get { return dateFlags; }
            set { dateFlags = value; }
        }

        public override string ToString()
        {
            return string.Format("Calendar - AnimeID: {0}...Release Date: {1}({2})...Flags: {3}", animeID,
                releaseDateRaw, releaseDate, dateFlags);
        }
    }

    public class CalendarCollection
    {
        private List<Calendar> calendars = new List<Calendar>();

        public List<Calendar> Calendars
        {
            get { return calendars; }
        }

        public CalendarCollection(string sRecMessage)
        {
            /*
			// 297 CALENDAR
			6622|1251417600|0
			6551|1251417600|0
			6652|1252108800|0
			6635|1252540800|0
			6698|1252627200|0
			6489|1253145600|0
			6653|1253750400|0
			6684|1253836800|0
			6781|1253836800|0
			6763|1253923200|0
			*/

            // DateFlags
            // 0 = normal start and end date (2010-01-31)
            // 1 = start date is year-month (2010-01)
            // 2 = start date is a year (2010)
            // 4 = normal start date, year-month end date 
            // 8 = normal start date, year end date 
            // 10 = start date is a year (2010)
            // 16 = normal start and end date (2010-01-31)


            // remove the header info
            string[] sDetails = sRecMessage.Substring(0).Split('\n');

            if (sDetails.Length <= 2) return;

            for (int i = 1; i < sDetails.Length - 1; i++)
                // first item will be the status command, and last will be empty
            {
                //BaseConfig.MyAnimeLog.Write("s: {0}", sDetails[i]);

                Calendar cal = new Calendar();

                string[] flds = sDetails[i].Substring(0).Split('|');
                cal.AnimeID = int.Parse(flds[0]);
                cal.ReleaseDateRaw = flds[1];
                cal.DateFlags = int.Parse(flds[2]);
                cal.ReleaseDate = Utils.GetAniDBDateAsDate(flds[1], cal.DateFlags);

                calendars.Add(cal);

                //BaseConfig.MyAnimeLog.Write("grp: {0}", grp);
            }
        }

        public override string ToString()
        {
            string ret = "";
            foreach (Calendar cal in calendars)
            {
                ret += cal.ToString() + Environment.NewLine;
            }
            return ret;
        }
    }
}