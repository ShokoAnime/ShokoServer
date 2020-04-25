namespace Shoko.Models.Server
{
    public class AniDB_ReleaseGroup
    {
        public int AniDB_ReleaseGroupID { get; set; }
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
    }
}