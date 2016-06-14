using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_AniDB_Anime_Similar
	{
		public int AniDB_Anime_SimilarID { get; set; }
		public int AnimeID { get; set; }
		public int SimilarAnimeID { get; set; }
		public int Approval { get; set; }
		public int Total { get; set; }

		public Contract_AniDBAnime AniDB_Anime { get; set; }
		public Contract_AnimeSeries AnimeSeries { get; set; }
	}
}
