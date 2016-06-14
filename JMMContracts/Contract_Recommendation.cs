using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_Recommendation
	{
		public int RecommendedAnimeID { get; set; }
		public int BasedOnAnimeID { get; set; }
		public double Score { get; set; }
		public int BasedOnVoteValue { get; set; }
		public double RecommendedApproval { get; set; }

		public Contract_AniDBAnime Recommended_AniDB_Anime { get; set; }
		public Contract_AnimeSeries Recommended_AnimeSeries { get; set; }

		public Contract_AniDBAnime BasedOn_AniDB_Anime { get; set; }
		public Contract_AnimeSeries BasedOn_AnimeSeries { get; set; }
	}
}
