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
	[XmlRoot("AddCrossRef_AniDB_TvDB_Request")]
	public class AddCrossRef_AniDB_TvDBRequest : XMLBase
	{
		protected string username = "";
		public string Username
		{
			get { return username; }
			set { username = value; }
		}

		protected string seriesName = "";
		public string SeriesName
		{
			get { return seriesName; }
			set { seriesName = value; }
		}

		protected int animeID = 0;
		public int AnimeID
		{
			get { return animeID; }
			set { animeID = value; }
		}

		protected int tvDBID = 0;
		public int TvDBID
		{
			get { return tvDBID; }
			set { tvDBID = value; }
		}

		protected int tvDBSeason = 0;
		public int TvDBSeason
		{
			get { return tvDBSeason; }
			set { tvDBSeason = value; }
		}

		// default constructor
		public AddCrossRef_AniDB_TvDBRequest()
		{
		}

		// default constructor
		public AddCrossRef_AniDB_TvDBRequest(CrossRef_AniDB_TvDB data, string seriesName)
		{
			this.AnimeID = data.AnimeID;
			this.TvDBID = data.TvDBID;
			this.TvDBSeason = data.TvDBSeasonNumber;

			string username = ServerSettings.AniDB_Username;
			if (ServerSettings.WebCache_Anonymous)
				username = Constants.AnonWebCacheUsername;

			this.Username = username;
			this.SeriesName = seriesName;
		}
	}
}
