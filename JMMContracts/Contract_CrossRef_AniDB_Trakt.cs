using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_CrossRef_AniDB_Trakt
	{
		public int CrossRef_AniDB_TraktID { get; set; }
		public int AnimeID { get; set; }
		public string TraktID { get; set; }
		public int TraktSeasonNumber { get; set; }
		public int CrossRefSource { get; set; }
	}
}
