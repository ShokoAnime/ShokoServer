using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class MetroContract_AniDB_Character
	{
		public int AniDB_CharacterID { get; set; }
		public int CharID { get; set; }
		public string CharName { get; set; }
		public string CharKanjiName { get; set; }
		public string CharDescription { get; set; }

		public int ImageType { get; set; }
		public int ImageID { get; set; }

		// from AniDB_Anime_Character
		public string CharType { get; set; }
	}
}
