using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMServer.Entities;

namespace JMMServer.Providers.Azure
{
    public class CrossRef_AniDB_TraktInput
    {
        public int AnimeID { get; set; }
        public string AnimeName { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public string TraktID { get; set; }
        public int TraktSeasonNumber { get; set; }
        public int TraktStartEpisodeNumber { get; set; }
        public string TraktTitle { get; set; }
        public int CrossRefSource { get; set; }
        public string Username { get; set; }
        public string AuthGUID { get; set; }  

		public CrossRef_AniDB_TraktInput()
		{
		}

        public CrossRef_AniDB_TraktInput(CrossRef_AniDB_TraktV2 xref, string animeName)
		{
			this.AnimeID = xref.AnimeID;
			this.AnimeName = animeName;
			this.AniDBStartEpisodeType = xref.AniDBStartEpisodeType;
			this.AniDBStartEpisodeNumber = xref.AniDBStartEpisodeNumber;
            this.TraktID = xref.TraktID;
            this.TraktSeasonNumber = xref.TraktSeasonNumber;
            this.TraktStartEpisodeNumber = xref.TraktStartEpisodeNumber;
            this.TraktTitle = xref.TraktTitle;
			this.CrossRefSource = xref.CrossRefSource;

			this.Username = ServerSettings.AniDB_Username;
			if (ServerSettings.WebCache_Anonymous)
				this.Username = Constants.AnonWebCacheUsername;

			this.AuthGUID = string.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;
		}
    }
}
