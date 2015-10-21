using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMDatabase;
using JMMDatabase.Extensions;
using JMMModels.Childs;
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

        public CrossRef_AniDB_MALInput(string userid, int animeId, AniDB_Anime_MAL xref)
        {
            JMMModels.JMMUser user = Store.JmmUserRepo.Find(userid);
            this.AnimeID = animeId;
            this.MALID = int.Parse(xref.MalId);
            this.CrossRefSource = (int)xref.CrossRefSource;
            this.MALTitle = xref.Title;
            this.StartEpisodeType = (int)xref.StartEpisodeType;
            this.StartEpisodeNumber = xref.StartEpisodeNumber;
            AniDBAuthorization auth = user.GetAniDBAuthorization();
            this.Username = Constants.AnonWebCacheUsername;
            if ((auth != null) && (!ServerSettings.WebCache_Anonymous))
                Username = auth.UserName;
		}
    }
}
