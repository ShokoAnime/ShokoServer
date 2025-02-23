namespace Shoko.Models.Azure
{
    public class Azure_AnimeDetail
    {
        // In Summary
        public int AnimeID { get; set; }
        public string AnimeName { get; set; }
        public long StartDateLong { get; set; }
        public long EndDateLong { get; set; }
        public string PosterURL { get; set; }

        // In Detail
        public string FanartURL { get; set; }
        public int EpisodeCountNormal { get; set; }
        public int EpisodeCountSpecial { get; set; }
        public decimal OverallRating { get; set; }
        public int TotalVotes { get; set; }
        public string AllCategories { get; set; }
        public string AllTags { get; set; }
        public string AnimeType { get; set; }
        public string Description { get; set; }
    }
}