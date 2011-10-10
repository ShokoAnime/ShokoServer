using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using JMMServer.Entities;
using AniDBAPI;
using JMMServer.Repositories;

namespace JMMServer.WebCache
{
	[Serializable]
	[XmlRoot("AddCrossRef_AniDB_Trakt_Request")]
	public class AddCrossRef_AniDB_TraktRequest : XMLBase
	{
		protected string username = "";
		public string Username
		{
			get { return username; }
			set { username = value; }
		}

		protected string showName = "";
		public string ShowName
		{
			get { return showName; }
			set { showName = value; }
		}

		protected int animeID = 0;
		public int AnimeID
		{
			get { return animeID; }
			set { animeID = value; }
		}

		protected string traktID = "";
		public string TraktID
		{
			get { return traktID; }
			set { traktID = value; }
		}

		protected int season = 0;
		public int Season
		{
			get { return season; }
			set { season = value; }
		}

		// default constructor
		public AddCrossRef_AniDB_TraktRequest()
		{
		}

		// default constructor
		public AddCrossRef_AniDB_TraktRequest(CrossRef_AniDB_Trakt data, string showName)
		{
			this.AnimeID = data.AnimeID;
			this.TraktID = data.TraktID;
			this.Season = data.TraktSeasonNumber;

			string username = ServerSettings.AniDB_Username;
			if (ServerSettings.WebCache_Anonymous)
				username = Constants.AnonWebCacheUsername;

			this.Username = username;
			this.ShowName = showName;
		}
	}
}
