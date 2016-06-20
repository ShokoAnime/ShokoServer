using System;
using JMMContracts;
using JMMServer.LZ4;
using JMMServer.Repositories;

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
        public byte[] ContractBlob { get; set; }
        public int ContractSize { get; set; }


        public const int CONTRACT_VERSION = 2;


        internal Contract_AnimeEpisode _contract = null;

        internal virtual Contract_AnimeEpisode Contract
        {
            get
            {
                if ((_contract == null) && (ContractBlob != null) && (ContractBlob.Length > 0) && (ContractSize > 0))
                    _contract = CompressionHelper.DeserializeObject<Contract_AnimeEpisode>(ContractBlob, ContractSize);
                if (_contract != null)
                {
                    AnimeSeries_UserRepository asrepo = new AnimeSeries_UserRepository();
                    AnimeSeries_User seuse = asrepo.GetByUserAndSeriesID(JMMUserID, AnimeSeriesID);
                    AnimeEpisodeRepository aerepo = new AnimeEpisodeRepository();
                    _contract.UnwatchedEpCountSeries = seuse?.UnwatchedEpisodeCount ?? 0;
                    AnimeEpisode aep = aerepo.GetByID(AnimeEpisodeID);
                    _contract.LocalFileCount = aep?.GetVideoLocals().Count ?? 0;
                }
                return _contract;
            }
            set
            {
                _contract = value;
                int outsize;
                ContractBlob = CompressionHelper.SerializeObject(value, out outsize);
                ContractSize = outsize;
                ContractVersion = CONTRACT_VERSION;
            }
        }

        public void CollectContractMemory()
        {
            _contract = null;
        }
    }
}