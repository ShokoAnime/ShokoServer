using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;

namespace JMMServer.WebCache
{
	public class CrossRef_AniDB_MALResult
	{
		public int AnimeID { get; set; }
		public int MALID { get; set; }
		public int CrossRefSource { get; set; }
		public string MALTitle { get; set; }
		public int StartEpisodeType { get; set; }
		public int StartEpisodeNumber { get; set; }

		// default constructor
		public CrossRef_AniDB_MALResult()
		{
		}

		public Contract_CrossRef_AniDB_MALResult ToContract()
		{
			Contract_CrossRef_AniDB_MALResult contract = new Contract_CrossRef_AniDB_MALResult();
			contract.AnimeID = this.AnimeID;
			contract.MALID = this.MALID;
			contract.CrossRefSource = this.CrossRefSource;
			contract.MALTitle = this.MALTitle;
			contract.StartEpisodeType = this.StartEpisodeType;
			contract.StartEpisodeNumber = this.StartEpisodeNumber;
			return contract;
		}
	}
}
