using JMMContracts;

namespace JMMContracts
{
	public class Contract_AnimeRating
	{
		public int AnimeID { get; set; }
		public Contract_AniDB_AnimeDetailed AnimeDetailed { get; set; }
		public Contract_AnimeSeries AnimeSeries { get; set; }
	}
}
