using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using NLog;
using System.IO;
using JMMServer.ImageDownload;
using JMMContracts;

namespace JMMServer.Entities
{
	public class CrossRef_AniDB_Trakt
	{
		public int CrossRef_AniDB_TraktID { get; private set; }
		public int AnimeID { get; set; }
		public string TraktID { get; set; }
		public int TraktSeasonNumber { get; set; }
		public int CrossRefSource { get; set; }

		public Contract_CrossRef_AniDB_Trakt ToContract()
		{
			Contract_CrossRef_AniDB_Trakt contract = new Contract_CrossRef_AniDB_Trakt();

			contract.CrossRef_AniDB_TraktID = CrossRef_AniDB_TraktID;
			contract.AnimeID = AnimeID;
			contract.TraktID = TraktID;
			contract.TraktSeasonNumber = TraktSeasonNumber;
			contract.CrossRefSource = CrossRefSource;

			return contract;
		}
	}
}
