using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;

namespace JMMServer.Entities
{
	public class CrossRef_AniDB_TvDB
	{
		public int CrossRef_AniDB_TvDBID { get; private set; }
		public int AnimeID { get; set; }
		public int TvDBID { get; set; }
		public int TvDBSeasonNumber { get; set; }
		public int CrossRefSource { get; set; }

		public Contract_CrossRef_AniDB_TvDB ToContract()
		{
			Contract_CrossRef_AniDB_TvDB contract = new Contract_CrossRef_AniDB_TvDB();
			contract.AnimeID = this.AnimeID;
			contract.TvDBID = this.TvDBID;
			contract.CrossRef_AniDB_TvDBID = this.CrossRef_AniDB_TvDBID;
			contract.TvDBSeasonNumber = this.TvDBSeasonNumber;
			contract.CrossRefSource = this.CrossRefSource;
			return contract;
		}
	}
}
