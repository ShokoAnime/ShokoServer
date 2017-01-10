namespace Shoko.Models.Metro
{
    public class Metro_Anime_Summary
    {
        public int AnimeID { get; set; }
        public int AnimeSeriesID { get; set; }
        public string AnimeName { get; set; }
        public int AirDateAsSeconds { get; set; }
        public int BeginYear { get; set; }
        public int EndYear { get; set; }
        public string PosterName { get; set; }
        public int UnwatchedEpisodeCount { get; set; }

        public int ImageType { get; set; }
        public int ImageID { get; set; }

        public string RelationshipType { get; set; }
        public string RelationshipDescription { get; set; }
    }
}