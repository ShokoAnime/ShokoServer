namespace JMMModels.Childs
{
    public class ProviderCrossRef
    {
        public string Title { get; set; }
        public AniDB_Episode_Type StartEpisodeType { get; set; }
        public int StartEpisodeNumber { get; set; }
        public CrossRefSourceType CrossRefSource { get; set; }
    }
}
