namespace JMMModels.Childs
{
    public class Episode_TraktEpisode : ImageInfo
    {
        public int Id { get; set; }
        public int ShowId { get; set; }
        public int Number { get; set; }
        public int Season { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string Overview { get; set; }
        public string EpisodeImage { get; set; }
    }
}
