using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;

namespace JMMServer.WebCache
{
	public class CrossRef_AniDB_OtherResult
	{
		public int AnimeID { get; set; }
		public string CrossRefID { get; set; }

		// default constructor
		public CrossRef_AniDB_OtherResult()
		{
		}

		public Contract_CrossRef_AniDB_OtherResult ToContract()
		{
			Contract_CrossRef_AniDB_OtherResult contract = new Contract_CrossRef_AniDB_OtherResult();
			contract.AnimeID = this.AnimeID;
			contract.CrossRefID = this.CrossRefID;
			return contract;
		}
	}
}
