using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMContracts.PlexAndKodi;
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

        public const int PLEXCONTRACT_VERSION = 3;


        private Video _plexcontract = null;
        internal virtual Video PlexContract
        {
            get
            {
                if ((_plexcontract == null) && PlexContractVersion == PLEXCONTRACT_VERSION)
                {
                    Video vids = Newtonsoft.Json.JsonConvert.DeserializeObject<Video>(PlexContractString);
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
