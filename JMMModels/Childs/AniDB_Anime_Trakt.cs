namespace JMMModels.Childs
{
    public class AniDB_Anime_Trakt : ProviderExtendedCrossRef
    {
        public string TraktId { get; set; }
        public int? Year { get; set; }
        public string Url { get; set; }
        public string Overview { get; set; }
        public int? TvDBId { get; set; }
        public string SeasonUrl { get; set; }
    }
    
}
