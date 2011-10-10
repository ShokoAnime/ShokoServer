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
	[XmlRoot("CrossRef_File_EpisodeRequest")]
	public class CrossRef_File_EpisodeRequest : XMLBase
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

		protected int animeID = 0;
		public int AnimeID
		{
			get { return animeID; }
			set { animeID = value; }
		}

		protected int episodeID = 0;
		public int EpisodeID
		{
			get { return episodeID; }
			set { episodeID = value; }
		}

		protected int percentage = 0;
		public int Percentage
		{
			get { return percentage; }
			set { percentage = value; }
		}

		protected int episodeOrder = 0;
		public int EpisodeOrder
		{
			get { return episodeOrder; }
			set { episodeOrder = value; }
		}
		

		// default constructor
		public CrossRef_File_EpisodeRequest()
		{
		}

		// default constructor
		public CrossRef_File_EpisodeRequest(CrossRef_File_Episode data)
		{
			this.AnimeID = data.AnimeID;
			this.EpisodeID = data.EpisodeID;
			this.Percentage = data.Percentage;
			this.EpisodeOrder = data.EpisodeOrder;
			this.hash = data.Hash;

			string username = ServerSettings.AniDB_Username;
			if (ServerSettings.WebCache_Anonymous)
				username = Constants.AnonWebCacheUsername;

			this.uname = username;
		}
	}
}
