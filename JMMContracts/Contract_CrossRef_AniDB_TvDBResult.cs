using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_CrossRef_AniDB_TvDBResult
	{
		public int AnimeID { get; set; }
		public int TvDBID { get; set; }
		public int TvDBSeasonNumber { get; set; }
		public int AdminApproved { get; set; }
		public string SeriesName { get; set; }
	}
}
