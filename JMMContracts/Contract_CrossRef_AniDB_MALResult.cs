using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_CrossRef_AniDB_MALResult
	{
		public int AnimeID { get; set; }
		public int MALID { get; set; }
		public int CrossRefSource { get; set; }
		public string MALTitle { get; set; }
		public int StartEpisodeType { get; set; }
		public int StartEpisodeNumber { get; set; }
	}
}
