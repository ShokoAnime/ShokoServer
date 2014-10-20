using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMServer.Entities;

namespace JMMServer.Providers
{
    public class CrossRef_AniDB_MALInput
    {
        public int AnimeID { get; set; }
        public int MALID { get; set; }
        public int CrossRefSource { get; set; }
        public string Username { get; set; }
        public string MALTitle { get; set; }
        public int StartEpisodeType { get; set; }
        public int StartEpisodeNumber { get; set; }

        public CrossRef_AniDB_MALInput()
		{
		}

        public CrossRef_AniDB_MALInput(CrossRef_AniDB_MAL xref)
		{
			this.AnimeID = xref.AnimeID;
            this.MALID = xref.MALID;
            this.CrossRefSource = xref.CrossRefSource;
            this.MALTitle = xref.MALTitle;
            this.StartEpisodeType = xref.StartEpisodeType;
            this.StartEpisodeNumber = xref.StartEpisodeNumber;

			this.Username = ServerSettings.AniDB_Username;
			if (ServerSettings.WebCache_Anonymous)
				this.Username = Constants.AnonWebCacheUsername;
		}
    }
}
