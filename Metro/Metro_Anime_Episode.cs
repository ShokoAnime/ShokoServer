namespace Shoko.Models.Metro
{
    public class Metro_Anime_Episode
    {
        public int AnimeEpisodeID { get; set; }
        public int EpisodeNumber { get; set; }
        public string EpisodeName { get; set; }
        public int EpisodeType { get; set; }
        public bool IsWatched { get; set; }
        public int LocalFileCount { get; set; }

        // from AniDB_Episode
        public int LengthSeconds { get; set; }
        public string AirDate { get; set; }

        // from TvDB
        public string EpisodeOverview { get; set; }
        public int ImageType { get; set; }
        public int ImageID { get; set; }
    }
}