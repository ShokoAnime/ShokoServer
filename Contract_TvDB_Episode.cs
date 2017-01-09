namespace Shoko.Models
{
    public class Contract_TvDB_Episode
    {
        public int TvDB_EpisodeID { get; set; }
        public int Id { get; set; }
        public int SeriesID { get; set; }
        public int SeasonID { get; set; }
        public int SeasonNumber { get; set; }
        public int EpisodeNumber { get; set; }
        public string EpisodeName { get; set; }
        public string Overview { get; set; }
        public string Filename { get; set; }
        public int EpImgFlag { get; set; }
        public int? AbsoluteNumber { get; set; }
        public int? AirsAfterSeason { get; set; }
        public int? AirsBeforeEpisode { get; set; }
        public int? AirsBeforeSeason { get; set; }
    }
}