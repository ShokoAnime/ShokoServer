using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
	}
}
