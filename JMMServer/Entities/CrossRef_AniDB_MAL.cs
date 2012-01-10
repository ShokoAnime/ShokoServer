using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using NLog;
using JMMContracts;

namespace JMMServer.Entities
{
	public class CrossRef_AniDB_MAL
	{
		public int CrossRef_AniDB_MALID { get; private set; }
		public int AnimeID { get; set; }
		public int MALID { get; set; }
		public string MALTitle { get; set; }
		public int StartEpisodeType { get; set; }
		public int StartEpisodeNumber { get; set; }
		public int CrossRefSource { get; set; }

		public Contract_CrossRef_AniDB_MAL ToContract()
		{
			Contract_CrossRef_AniDB_MAL contract = new Contract_CrossRef_AniDB_MAL();

			contract.CrossRef_AniDB_MALID = CrossRef_AniDB_MALID;
			contract.AnimeID = AnimeID;
			contract.MALID = MALID;
			contract.MALTitle = MALTitle;
			contract.StartEpisodeType = StartEpisodeType;
			contract.StartEpisodeNumber = StartEpisodeNumber;
			contract.CrossRefSource = CrossRefSource;

			return contract;
		}
	}
}
