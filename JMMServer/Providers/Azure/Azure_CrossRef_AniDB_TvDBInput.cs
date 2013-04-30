using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;

namespace JMMServer.Providers.Azure
{
	public class CrossRef_AniDB_TvDBInput
	{
		public int AnimeID { get; set; }
		public string AnimeName { get; set; }
		public int AniDBStartEpisodeType { get; set; }
		public int AniDBStartEpisodeNumber { get; set; }
		public int TvDBID { get; set; }
		public int TvDBSeasonNumber { get; set; }
		public int TvDBStartEpisodeNumber { get; set; }
		public string TvDBTitle { get; set; }
		public int CrossRefSource { get; set; }
		public string Username { get; set; }
		public string AuthGUID { get; set; }

		public CrossRef_AniDB_TvDBInput()
		{
		}

		public CrossRef_AniDB_TvDBInput(CrossRef_AniDB_TvDBV2 xref, string animeName)
		{
			this.AnimeID = xref.AnimeID;
			this.AnimeName = animeName;
			this.AniDBStartEpisodeType = xref.AniDBStartEpisodeType;
			this.AniDBStartEpisodeNumber = xref.AniDBStartEpisodeNumber;
			this.TvDBID = xref.TvDBID;
			this.TvDBSeasonNumber = xref.TvDBSeasonNumber;
			this.TvDBStartEpisodeNumber = xref.TvDBStartEpisodeNumber;
			this.TvDBTitle = xref.TvDBTitle;
			this.CrossRefSource = xref.CrossRefSource;

			this.Username = ServerSettings.AniDB_Username;
			if (ServerSettings.WebCache_Anonymous)
				this.Username = Constants.AnonWebCacheUsername;

			this.AuthGUID = ServerSettings.WebCacheAuthKey;
		}
	}
}
