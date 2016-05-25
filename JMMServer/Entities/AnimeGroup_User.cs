using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMContracts.PlexAndKodi;
using JMMServer.Repositories;
using Newtonsoft.Json;
using NLog;

namespace JMMServer.Entities
{
	public class AnimeGroup_User
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		#region DB Columns
		public int AnimeGroup_UserID { get; private set; }
		public int JMMUserID { get; set; }
		public int AnimeGroupID { get; set; }
		public int IsFave { get; set; }
		public int UnwatchedEpisodeCount { get; set; }
		public int WatchedEpisodeCount { get; set; }
		public DateTime? WatchedDate { get; set; }
		public int PlayedCount { get; set; }
		public int WatchedCount { get; set; }
		public int StoppedCount { get; set; }

        public int PlexContractVersion { get; set; }
        public string PlexContractString { get; set; }

        #endregion

        public const int PLEXCONTRACT_VERSION = 3;


        private Video _plexcontract = null;
        public Video PlexContract
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



        public AnimeGroup_User()
		{
		}

		public AnimeGroup_User(int userID, int groupID)
		{
			JMMUserID = userID;
			AnimeGroupID = groupID;
			IsFave = 0;
			UnwatchedEpisodeCount = 0;
			WatchedEpisodeCount = 0;
			WatchedDate = null;
			PlayedCount = 0;
			WatchedCount = 0;
			StoppedCount = 0;
		}

		

		public bool HasUnwatchedFiles => UnwatchedEpisodeCount > 0;

	    public bool AllFilesWatched => UnwatchedEpisodeCount == 0;

	    public bool AnyFilesWatched => WatchedEpisodeCount > 0;
	}
}
