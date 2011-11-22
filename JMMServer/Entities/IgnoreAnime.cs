using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMServer.Repositories;

namespace JMMServer.Entities
{
	public class IgnoreAnime
	{
		public int IgnoreAnimeID { get; private set; }
		public int JMMUserID { get; set; }
		public int AnimeID { get; set; }
		public int IgnoreType { get; set; }

		public override string ToString()
		{
			return string.Format("User: {0} - Anime: {1} - Type: {2}", JMMUserID, AnimeID, IgnoreType);
		}

		public Contract_IgnoreAnime ToContract()
		{
			Contract_IgnoreAnime contract = new Contract_IgnoreAnime();

			contract.IgnoreAnimeID = this.IgnoreAnimeID;
			contract.JMMUserID = this.JMMUserID;
			contract.AnimeID = this.AnimeID;
			contract.IgnoreType = this.IgnoreType;

			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			AniDB_Anime anime = repAnime.GetByAnimeID(AnimeID);
			if (anime != null) contract.Anime = anime.ToContract();

			return contract;
		}
	}
}
