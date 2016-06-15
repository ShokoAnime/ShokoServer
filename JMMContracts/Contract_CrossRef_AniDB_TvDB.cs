namespace JMMContracts
{
    public class Contract_CrossRef_AniDB_TvDB
    {
        public int CrossRef_AniDB_TvDBID { get; set; }
        public int AnimeID { get; set; }
        public int TvDBID { get; set; }
        public int TvDBSeasonNumber { get; set; }
        public int CrossRefSource { get; set; }
    }
}