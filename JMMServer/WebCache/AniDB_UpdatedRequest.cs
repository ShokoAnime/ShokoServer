using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using AniDBAPI;

namespace JMMServer.WebCache
{
	[Serializable]
	[XmlRoot("AniDB_Updated")]
	public class AniDB_UpdatedRequest : XMLBase
	{
		public long UpdatedTime { get; set; }
		public string Username { get; set; }
		public string AnimeIDList { get; set; }
		
		// default constructor
		public AniDB_UpdatedRequest()
		{
		}

		// default constructor
		public AniDB_UpdatedRequest(string uptime, string aidlist)
		{
			this.UpdatedTime = long.Parse(uptime);
			this.AnimeIDList = aidlist;

			string username = ServerSettings.AniDB_Username;
			if (ServerSettings.WebCache_Anonymous)
				username = Constants.AnonWebCacheUsername;

			this.Username = username;
		}
	}
}
