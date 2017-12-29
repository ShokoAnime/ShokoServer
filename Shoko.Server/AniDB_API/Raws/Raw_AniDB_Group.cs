using System.Text;

namespace AniDBAPI
{
    public class Raw_AniDB_Group : XMLBase
    {
        public int GroupID { get; set; }
        public int Rating { get; set; }
        public int Votes { get; set; }
        public int AnimeCount { get; set; }
        public int FileCount { get; set; }
        public string GroupName { get; set; }
        public string GroupNameShort { get; set; }
        public string IRCChannel { get; set; }
        public string IRCServer { get; set; }
        public string URL { get; set; }
        public string Picname { get; set; }

        private void PopulateDefaults()
        {
            GroupID = 0;
            Rating = 0;
            Rating = 0;
            Votes = 0;
            AnimeCount = 0;
            FileCount = 0;
            GroupName = string.Empty;
            GroupNameShort = string.Empty;
            IRCChannel = string.Empty;
            IRCServer = string.Empty;
            URL = string.Empty;
            Picname = string.Empty;
        }

        public Raw_AniDB_Group()
        {
            PopulateDefaults();
        }

        public Raw_AniDB_Group(string sRecMessage)
        {
            PopulateDefaults();

            // remove the header info
            string[] sDetails = sRecMessage.Substring(10).Split('|');

            //250 GROUP
            //  0. 3938 ** group id
            //  1. 704 ** rating
            //  2. 1900 ** votes
            //  3. 53 ** anime count
            //  4. 1126 ** file count
            //  5. Ayako-Fansubs ** group name
            //  6. Ayako ** short name
            //  7. #Ayako ** IRC channel
            //  8. irc.rizon.net ** IRC server
            //  9. http://ayakofansubs.info/ ** website
            // 10. 1669.png

            GroupID = int.Parse(sDetails[0]);
            Rating = int.Parse(sDetails[1]);
            Votes = int.Parse(sDetails[2]);
            AnimeCount = int.Parse(sDetails[3]);
            FileCount = int.Parse(sDetails[4]);

            GroupName = AniDBAPILib.ProcessAniDBString(sDetails[5]);
            GroupNameShort = AniDBAPILib.ProcessAniDBString(sDetails[6]);
            IRCChannel = AniDBAPILib.ProcessAniDBString(sDetails[7]);
            IRCServer = AniDBAPILib.ProcessAniDBString(sDetails[8]);
            URL = AniDBAPILib.ProcessAniDBString(sDetails[9]);
            Picname = AniDBAPILib.ProcessAniDBString(sDetails[10]);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("AniDB_Group:: AnimeID: " + GroupID.ToString());
            sb.Append(" | GroupName: " + GroupName);
            sb.Append(" | URL: " + URL);

            return sb.ToString();
        }
    }
}