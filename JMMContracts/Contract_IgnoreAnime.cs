using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_IgnoreAnime
	{
		public int IgnoreAnimeID { get; set; }
		public int JMMUserID { get; set; }
		public int AnimeID { get; set; }
		public int IgnoreType { get; set; }

		public Contract_AniDBAnime Anime { get; set; }
	}
}
