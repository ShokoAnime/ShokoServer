using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using JMMServer.Entities;
using AniDBAPI;

namespace JMMServer.WebCache
{
	[Serializable]
	[XmlRoot("DeleteCrossRef_AniDB_OtherRequest")]
	public class DeleteCrossRef_AniDB_OtherRequest : XMLBase
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

		protected int crossRefType = 0;
		public int CrossRefType
		{
			get { return crossRefType; }
			set { crossRefType = value; }
		}

		// default constructor
		public DeleteCrossRef_AniDB_OtherRequest()
		{
		}

		// default constructor
		public DeleteCrossRef_AniDB_OtherRequest(int animeID, CrossRefType xrefType)
		{
			this.AnimeID = animeID;
			this.CrossRefType = (int)xrefType;

			string username = ServerSettings.AniDB_Username;
			if (ServerSettings.WebCache_Anonymous)
				username = Constants.AnonWebCacheUsername;

			this.Username = username;
		}
	}
}
