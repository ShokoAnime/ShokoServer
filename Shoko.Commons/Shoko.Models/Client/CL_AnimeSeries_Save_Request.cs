namespace Shoko.Models.Client
{
    public class CL_AnimeSeries_Save_Request
    {
        public int? AnimeSeriesID { get; set; }
        public int AnimeGroupID { get; set; }
        public int AniDB_ID { get; set; }
        public string DefaultAudioLanguage { get; set; }
        public string DefaultSubtitleLanguage { get; set; }
        public string SeriesNameOverride { get; set; }

        public string DefaultFolder { get; set; }
    }
}