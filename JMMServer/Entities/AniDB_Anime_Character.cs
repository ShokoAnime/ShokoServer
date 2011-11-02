using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;
using JMMContracts;
using JMMServer.Repositories;

namespace JMMServer.Entities
{
	public class AniDB_Anime_Character
	{
		public int AniDB_Anime_CharacterID { get; private set; }
		public int AnimeID { get; set; }
		public int CharID { get; set; }
		public string CharType { get; set; }
		public string EpisodeListRaw { get; set; }

		public void Populate(Raw_AniDB_Character rawChar)
		{
			this.CharID = rawChar.CharID;
			this.AnimeID = rawChar.AnimeID;
			this.CharType = rawChar.CharType;
			this.EpisodeListRaw = rawChar.EpisodeListRaw;
		}

		public AniDB_Character Character
		{
			get
			{
				AniDB_CharacterRepository repChar = new AniDB_CharacterRepository();
				return repChar.GetByCharID(CharID);
			}
		}

		public Contract_AniDB_Anime_Character ToContract()
		{
			Contract_AniDB_Anime_Character contract = new Contract_AniDB_Anime_Character();

			contract.AniDB_Anime_CharacterID = this.AniDB_Anime_CharacterID;
			contract.AnimeID = this.AnimeID;
			contract.CharID = this.CharID;
			contract.CharType = this.CharType;
			contract.EpisodeListRaw = this.EpisodeListRaw;

			return contract;
		}
	}
}
