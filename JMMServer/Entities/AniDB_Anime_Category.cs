using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;

namespace JMMServer.Entities
{
	public class AniDB_Anime_Category
	{
		public int AniDB_Anime_CategoryID { get; private set; }
		public int AnimeID { get; set; }
		public int CategoryID { get; set; }
		public int Weighting { get; set; }

		public void Populate(Raw_AniDB_Category rawCat)
		{
			this.AnimeID = rawCat.AnimeID;
			this.CategoryID = rawCat.CategoryID;
			this.Weighting = rawCat.Weighting;
		}

		public override string ToString()
		{
			return string.Format("AniDB_Anime_Category: {0}/{1} = {2}", AnimeID, CategoryID, Weighting);
		}
	}
}
