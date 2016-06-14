using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;
using JMMContracts;

namespace JMMServer.Entities
{
	public class AniDB_Anime_Relation
	{
		public int AniDB_Anime_RelationID { get; private set; }
		public int AnimeID { get; set; }
		public string RelationType { get; set; }
		public int RelatedAnimeID { get; set; }

		public void Populate(Raw_AniDB_RelatedAnime rawRel)
		{
			this.AnimeID = rawRel.AnimeID;
			this.RelatedAnimeID = rawRel.RelatedAnimeID;
			this.RelationType = rawRel.RelationType;
		}

		public Contract_AniDB_Anime_Relation ToContract(AniDB_Anime anime, AnimeSeries ser, int userID)
		{
			Contract_AniDB_Anime_Relation contract = new Contract_AniDB_Anime_Relation();

			contract.AniDB_Anime_RelationID = this.AniDB_Anime_RelationID;
			contract.AnimeID = this.AnimeID;
			contract.RelationType = this.RelationType;
			contract.RelatedAnimeID = this.RelatedAnimeID;

			contract.AniDB_Anime = null;
			if (anime != null)
				contract.AniDB_Anime = anime.ToContract();

			contract.AnimeSeries = null;
			if (ser != null)
				contract.AnimeSeries = ser.ToContract(ser.GetUserRecord(userID));

			return contract;
		}
	}
}
