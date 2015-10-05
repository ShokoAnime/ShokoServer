using System;

namespace JMMModels.Childs
{
    public class Episode_TvDBEpisode : ImageInfo
    {
        public int Id { get; set; }
        public int SeriesId { get; set; }
        public int SeasonId { get; set; }
        public int SeasonNumber { get; set; }
        public int EpisodeNumber { get; set; }
        public string EpisodeName { get; set; }
        public string Overview { get; set; }
        public string Filename { get; set; }
        public int EpImgFlag { get; set; }
        public DateTime? FirstAired { get; set; }
        public int? AbsoluteNumber { get; set; }
        public int? AirsAfterSeason { get; set; }
        public int? AirsBeforeEpisode { get; set; }
        public int? AirsBeforeSeason { get; set; }
    }
}
