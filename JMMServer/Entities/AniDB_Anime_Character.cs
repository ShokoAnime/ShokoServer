using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AniDBAPI;

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
	}
}
