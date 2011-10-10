using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;

namespace JMMServer.Entities
{
	public class AniDB_Anime_Relation
	{
		public int AniDB_Anime_RelationID { get; set; }
		public int AnimeID { get; set; }
		public string RelationType { get; set; }
		public int RelatedAnimeID { get; set; }

		public void Populate(Raw_AniDB_RelatedAnime rawRel)
		{
			this.AnimeID = rawRel.AnimeID;
			this.RelatedAnimeID = rawRel.RelatedAnimeID;
			this.RelationType = rawRel.RelationType;
		}
	}
}
