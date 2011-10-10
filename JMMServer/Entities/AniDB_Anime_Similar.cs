using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;

namespace JMMServer.Entities
{
	public class AniDB_Anime_Similar
	{
		public int AniDB_Anime_SimilarID { get; private set; }
		public int AnimeID { get; set; }
		public int SimilarAnimeID { get; set; }
		public int Approval { get; set; }
		public int Total { get; set; }


		public void Populate(Raw_AniDB_SimilarAnime rawSim)
		{
			this.AnimeID = rawSim.AnimeID;
			this.Approval = rawSim.Approval;
			this.Total = rawSim.Total;
			this.SimilarAnimeID = rawSim.SimilarAnimeID;

		}
	}
}
