namespace Shoko.Models.Server
{
    public class CrossRef_AniDB_Trakt_Episode 
    {
        public int CrossRef_AniDB_Trakt_EpisodeID { get;  set; }
        public int AnimeID { get; set; }
        public int AniDBEpisodeID { get; set; }
        public string TraktID { get; set; }
        public int Season { get; set; }
        public int EpisodeNumber { get; set; }

    }
}