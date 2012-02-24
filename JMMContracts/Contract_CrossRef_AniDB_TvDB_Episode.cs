using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_CrossRef_AniDB_TvDB_Episode
	{
		public int CrossRef_AniDB_TvDB_EpisodeID { get; set; }
		public int AnimeID { get; set; }
		public int AniDBEpisodeID { get; set; }
		public int TvDBEpisodeID { get; set; }
	}
}
