namespace JMMContracts
{
    public class Contract_Azure_CrossRef_AniDB_Trakt
    {
        public int AnimeID { get; set; }
        public string AnimeName { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public string TraktID { get; set; }
        public int TraktSeasonNumber { get; set; }
        public int TraktStartEpisodeNumber { get; set; }
        public string TraktTitle { get; set; }
        public int CrossRefSource { get; set; }
        public string Username { get; set; }
        public int IsAdminApproved { get; set; }
        public long DateSubmitted { get; set; }

        public int? CrossRef_AniDB_TraktId { get; set; }

        public Contract_Azure_CrossRef_AniDB_Trakt()
        {
        }
    }
}