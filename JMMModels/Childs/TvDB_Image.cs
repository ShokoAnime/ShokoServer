namespace JMMModels.Childs
{
    public class TvDB_Image : ImageInfo
    {
        public int Id { get; set; }
        public string BannerPath { get; set; }

        public float Rating { get; set; }
        public int RatingCount { get; set; }
        public string Colors { get; set; }
        public int? Season { get; set; }
        public bool IsSeriesName { get; set; }
        public string Language { get; set; }
        public string ThumbnailPath { get; set; }
        public string VignettePath { get; set; }
        public bool Chosen { get; set; }
    }
}
