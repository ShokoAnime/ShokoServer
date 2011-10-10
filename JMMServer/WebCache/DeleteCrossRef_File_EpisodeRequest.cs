using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using AniDBAPI;
using JMMServer.Entities;

namespace JMMServer.WebCache
{
	[Serializable]
	[XmlRoot("DeleteCrossRef_File_EpisodeRequest")]
	public class DeleteCrossRef_File_EpisodeRequest : XMLBase
	{
		protected string uname = "";
		public string Uname
		{
			get { return uname; }
			set { uname = value; }
		}

		protected string hash = "";
		public string Hash
		{
			get { return hash; }
			set { hash = value; }
		}

		protected int episodeID = 0;
		public int EpisodeID
		{
			get { return episodeID; }
			set { episodeID = value; }
		}

		// default constructor
		public DeleteCrossRef_File_EpisodeRequest()
		{
		}

		// default constructor
		public DeleteCrossRef_File_EpisodeRequest(string hash, int aniDBEpisodeID)
		{
			this.EpisodeID = aniDBEpisodeID;
			this.hash = hash;

			string username = ServerSettings.AniDB_Username;
			if (ServerSettings.WebCache_Anonymous)
				username = Constants.AnonWebCacheUsername;

			this.uname = username;
		}
	}
}
