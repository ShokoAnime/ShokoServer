namespace JMMContracts
{
	public class Contract_Azure_CrossRef_AniDB_TvDB
	{
		public int AnimeID { get; set; }
		public string AnimeName { get; set; }
		public int AniDBStartEpisodeType { get; set; }
		public int AniDBStartEpisodeNumber { get; set; }
		public int TvDBID { get; set; }
		public int TvDBSeasonNumber { get; set; }
		public int TvDBStartEpisodeNumber { get; set; }
		public string TvDBTitle { get; set; }
		public int CrossRefSource { get; set; }
		public string Username { get; set; }
		public int IsAdminApproved { get; set; }
		public long DateSubmitted { get; set; }

        public int? CrossRef_AniDB_TvDBId { get; set; }

		public Contract_Azure_CrossRef_AniDB_TvDB()
		{
		}
	}
}
