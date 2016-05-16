using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMServer.Repositories;
using JMMServer.Commands;
using AniDBAPI;
using Newtonsoft.Json;

namespace JMMServer.Entities
{
	public class AnimeEpisode_User
	{
		public int AnimeEpisode_UserID { get; private set; }
		public int JMMUserID { get; set; }
		public int AnimeEpisodeID { get; set; }
		public int AnimeSeriesID { get; set; }
		public DateTime? WatchedDate { get; set; }
		public int PlayedCount { get; set; }
		public int WatchedCount { get; set; }
		public int StoppedCount { get; set; }

        public int ContractVersion { get; set; }
        public string ContractString { get; set; }


        public const int CONTRACT_VERSION = 1;


        internal Contract_AnimeEpisode _contract = null;
        public virtual Contract_AnimeEpisode Contract
        {
            get
            {
                if ((_contract == null) && (ContractVersion == CONTRACT_VERSION))
                    _contract = Newtonsoft.Json.JsonConvert.DeserializeObject<Contract_AnimeEpisode>(ContractString);
                if (_contract != null)
                {
                    AnimeSeries_UserRepository asrepo = new AnimeSeries_UserRepository();
                    AnimeSeries_User seuse = asrepo.GetByUserAndSeriesID(JMMUserID, AnimeSeriesID);
                    AnimeEpisodeRepository aerepo=new AnimeEpisodeRepository();
                    _contract.UnwatchedEpCountSeries = seuse?.UnwatchedEpisodeCount ?? 0;
                    AnimeEpisode aep =aerepo.GetByID(AnimeEpisodeID);
                    _contract.LocalFileCount=aep?.GetVideoLocals().Count ?? 0;
                }
                return _contract;
            }
            set
            {
                _contract = value;
                if (value != null)
                {
                    ContractVersion = CONTRACT_VERSION;
                    ContractString = JsonConvert.SerializeObject(value);
                }
            }
        }

    }
}
