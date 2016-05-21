using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMServer.Plex;
using JMMServer.Repositories;
using Newtonsoft.Json;

namespace JMMServer.Entities
{
	public class AnimeSeries_User
	{
		public int AnimeSeries_UserID { get; private set; }
		public int JMMUserID { get; set; }
		public int AnimeSeriesID { get; set; }

		public int UnwatchedEpisodeCount { get; set; }
		public int WatchedEpisodeCount { get; set; }
		public DateTime? WatchedDate { get; set; }
		public int PlayedCount { get; set; }
		public int WatchedCount { get; set; }
		public int StoppedCount { get; set; }


        public int PlexContractVersion { get; set; }
        public string PlexContractString { get; set; }

        public int KodiContractVersion { get; set; }
        public string KodiContractString { get; set; }


        public const int PLEXCONTRACT_VERSION = 2;
        public const int KODICONTRACT_VERSION = 2;


        private JMMContracts.PlexContracts.Video _plexcontract = null;
        internal virtual JMMContracts.PlexContracts.Video PlexContract
        {
            get
            {
                if ((_plexcontract == null) && PlexContractVersion == PLEXCONTRACT_VERSION)
                {
                    JMMContracts.PlexContracts.Video vids = Newtonsoft.Json.JsonConvert.DeserializeObject<JMMContracts.PlexContracts.Video>(PlexContractString);
                    if (vids != null)
                        _plexcontract = vids;
                }
                return _plexcontract;
            }
            set
            {
                _plexcontract = value;
                if (value != null)
                {
                    PlexContractVersion = AnimeGroup_User.PLEXCONTRACT_VERSION;
                    PlexContractString = Newtonsoft.Json.JsonConvert.SerializeObject(PlexContract, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
                }
            }
        }


        private JMMContracts.KodiContracts.Video _kodicontract = null;
        internal virtual JMMContracts.KodiContracts.Video KodiContract
        {
            get
            {
                if ((_kodicontract == null) && KodiContractVersion == KODICONTRACT_VERSION)
                {
                    JMMContracts.KodiContracts.Video vids = Newtonsoft.Json.JsonConvert.DeserializeObject<JMMContracts.KodiContracts.Video>(KodiContractString);
                    if (vids != null)
                        _kodicontract = vids;
                }
                return _kodicontract;
            }
            set
            {
                _kodicontract = value;
                if (value != null)
                {
                    KodiContractVersion = AnimeGroup_User.KODICONTRACT_VERSION;
                    KodiContractString = Newtonsoft.Json.JsonConvert.SerializeObject(KodiContract, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
                }
            }
        }


        public AnimeSeries_User()
		{
		}

		public AnimeSeries_User(int userID, int seriesID)
		{
			JMMUserID = userID;
			AnimeSeriesID = seriesID;
			UnwatchedEpisodeCount = 0;
			WatchedEpisodeCount = 0;
			WatchedDate = null;
			PlayedCount = 0;
			WatchedCount = 0;
			StoppedCount = 0;
		}
	}
}
