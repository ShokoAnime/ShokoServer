using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;

namespace JMMServer.Entities
{
	public class CrossRef_AniDB_Other
	{
		public int CrossRef_AniDB_OtherID { get; private set; }
		public int AnimeID { get; set; }
		public string CrossRefID { get; set; }
		public int CrossRefSource { get; set; }
		public int CrossRefType { get; set; }

		public Contract_CrossRef_AniDB_Other ToContract()
		{
			Contract_CrossRef_AniDB_Other contract = new Contract_CrossRef_AniDB_Other();
			contract.AnimeID = this.AnimeID;
			contract.CrossRefID = this.CrossRefID;
			contract.CrossRefSource = this.CrossRefSource;
			contract.CrossRefType = this.CrossRefType;
			return contract;
		}
	}
}
