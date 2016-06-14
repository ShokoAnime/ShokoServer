using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_CrossRef_AniDB_Other
	{
		public int CrossRef_AniDB_OtherID { get; set; }
		public int AnimeID { get; set; }
		public string CrossRefID { get; set; }
		public int CrossRefSource { get; set; }
		public int CrossRefType { get; set; }
	}
}
