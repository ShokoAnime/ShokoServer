namespace Shoko.Server.Providers.AniDB.UDP.MyList.Responses
{
    public class ResponseMyListStats
    {
        public int Anime { get; set; }
        public int Episodes { get; set; }
        public int Files { get; set; }
        public long SizeOfFiles { get; set; }
        public int AddedAnime { get; set; }
        public int AddedEpisodes { get; set; }
        public int AddedFiles { get; set; }
        public int AddedGroups { get; set; }
        public int LeechPercent { get; set; }
        public int GloryPercent { get; set; }
        public int ViewedPercent { get; set; }
        public int MyListPercent { get; set; }
        public int ViewedMyListPercent { get; set; }
        public int EpisodesViewed { get; set; }
        public int Votes { get; set; }
        public int Reviews { get; set; }
        public long ViewedLength { get; set; }
    }
}
