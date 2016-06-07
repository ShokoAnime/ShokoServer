using System;
using System.Xml;
using NLog;

namespace AniDBAPI
{
    public class Raw_AniDB_MyListFile
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // default constructor
        public Raw_AniDB_MyListFile()
        {
        }

        // constructor
        // sRecMessage is the message received from ANIDB file info command
        public Raw_AniDB_MyListFile(string sRecMessage)
        {
            // remove the header info
            var sDetails = sRecMessage.Substring(11).Split('|');

            // 221 MYLIST

            //{int4 lid}|{int4 fid}|{int4 eid}|{int4 aid}|{int4 gid}|{int4 date}|{int2 state}|{int4 viewdate}|{str storage}|{str source}|{str other}|{int2 filestate} 
            //  0. 66031082  ** list id
            //  1. 572794  ** file id
            //  2. 99294 ** episode id
            //  3. 6107 ** anime id
            //  4. 2723 ** group id
            //  5. 1239598714 ** fileDate
            //  6. 1 ** state
            //  7. 0  ** view date (will contain 0 if not watched, otherwise has the date it was watched)
            //  8.    ** storage
            //  9.    ** source
            // 10.    ** other
            // 11. 0  ** filestate

            ListID = AniDBAPILib.ProcessAniDBInt(sDetails[0]);
            FileID = AniDBAPILib.ProcessAniDBInt(sDetails[1]);
            EpisodeID = AniDBAPILib.ProcessAniDBInt(sDetails[2]);
            AnimeID = AniDBAPILib.ProcessAniDBInt(sDetails[3]);
            GroupID = AniDBAPILib.ProcessAniDBInt(sDetails[4]);
            FileDate = AniDBAPILib.ProcessAniDBString(sDetails[5]);
            State = AniDBAPILib.ProcessAniDBInt(sDetails[6]);
            ViewDateUDP = AniDBAPILib.ProcessAniDBInt(sDetails[7]);
            Storage = AniDBAPILib.ProcessAniDBString(sDetails[8]);
            Source = AniDBAPILib.ProcessAniDBString(sDetails[9]);
            Other = AniDBAPILib.ProcessAniDBString(sDetails[10]);
            FileState = AniDBAPILib.ProcessAniDBInt(sDetails[11]);

            // calculate the watched date
            if (ViewDateUDP > 0)
            {
                var utcDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                utcDate = utcDate.AddSeconds(ViewDateUDP);

                WatchedDate = utcDate.ToLocalTime();
            }
        }

        public int ListID { get; set; }
        public int FileID { get; set; }
        public int EpisodeID { get; set; }
        public int AnimeID { get; set; }
        public int GroupID { get; set; }
        public int State { get; set; }
        public string FileDate { get; set; }
        public DateTime? WatchedDate { get; set; }
        public int ViewDateUDP { get; set; }
        public string ViewDateHTTP { get; set; }
        public string Storage { get; set; }
        public string Source { get; set; }
        public string Other { get; set; }
        public int FileState { get; set; }

        public bool IsWatched
        {
            get
            {
                if (ViewDateUDP > 0 || ViewDateHTTP.Trim().Length > 0)
                    return true;
                return false;
            }
        }

        private void InitVals()
        {
            ListID = 0;
            FileID = 0;
            EpisodeID = 0;
            AnimeID = 0;
            GroupID = 0;
            FileDate = "";
            ViewDateUDP = 0;
            ViewDateHTTP = "";
            Storage = "";
            Source = "";
            Other = "";
            FileState = 0;
            WatchedDate = null;
        }

        public void ProcessHTTPSource(XmlNode node)
        {
            ListID = int.Parse(node.Attributes["id"].Value);
            AnimeID = int.Parse(node.Attributes["aid"].Value);
            EpisodeID = int.Parse(node.Attributes["eid"].Value);
            FileID = int.Parse(node.Attributes["fid"].Value);

            ViewDateHTTP = AniDBHTTPHelper.TryGetAttribute(node, "viewdate");

            // calculate the watched date
            if (!string.IsNullOrEmpty(ViewDateHTTP) && ViewDateHTTP.Length > 17)
            {
                try
                {
                    // eg "2011-02-23T20:49:18+0000"
                    var year = int.Parse(ViewDateHTTP.Trim().Substring(0, 4));
                    var month = int.Parse(ViewDateHTTP.Trim().Substring(5, 2));
                    var day = int.Parse(ViewDateHTTP.Trim().Substring(8, 2));

                    var hour = int.Parse(ViewDateHTTP.Trim().Substring(11, 2));
                    var minute = int.Parse(ViewDateHTTP.Trim().Substring(14, 2));
                    var second = int.Parse(ViewDateHTTP.Trim().Substring(17, 2));

                    var utcDate = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
                    utcDate = utcDate.AddSeconds(ViewDateUDP);

                    WatchedDate = utcDate.ToLocalTime();
                }
                catch (Exception ex)
                {
                    logger.ErrorException("Error processing View Date HTTP: " + ex, ex);
                }
            }

            var tempstate = AniDBHTTPHelper.TryGetProperty(node, "state");
            var istate = 0;
            int.TryParse(tempstate, out istate);
            State = istate;

            var fstate = AniDBHTTPHelper.TryGetProperty(node, "filestate");
            var ifilestate = 0;
            int.TryParse(fstate, out ifilestate);
            FileState = ifilestate;

            Source = AniDBHTTPHelper.TryGetProperty(node, "storage");
        }

        public override string ToString()
        {
            return string.Format("Raw_AniDB_MyListFile:: fileID: {0} | IsWatched: {1}", FileID, IsWatched);
        }
    }
}