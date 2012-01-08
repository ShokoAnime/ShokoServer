using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using AniDBAPI;

namespace JMMServer.WebCache
{
	[Serializable]
	[XmlRoot("DeleteCrossRef_AniDB_MALRequest")]
	public class DeleteCrossRef_AniDB_MALRequest : XMLBase
	{
		protected string username = "";
		public string Username
		{
			get { return username; }
			set { username = value; }
		}

		protected int animeID = 0;
		public int AnimeID
		{
			get { return animeID; }
			set { animeID = value; }
		}

		// default constructor
		public DeleteCrossRef_AniDB_MALRequest()
		{
		}

		// default constructor
		public DeleteCrossRef_AniDB_MALRequest(int animeID)
		{
			this.AnimeID = animeID;

			string username = ServerSettings.AniDB_Username;
			if (ServerSettings.WebCache_Anonymous)
				username = Constants.AnonWebCacheUsername;

			this.Username = username;
		}
	}
}
