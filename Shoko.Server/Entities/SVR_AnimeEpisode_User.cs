using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.LZ4;
using Shoko.Server.Repositories;

namespace Shoko.Server.Entities
{
    public class SVR_AnimeEpisode_User : AnimeEpisode_User
    {
        public SVR_AnimeEpisode_User()
        {
        }
        public int ContractVersion { get; set; }
        public byte[] ContractBlob { get; set; }
        public int ContractSize { get; set; }


        public const int CONTRACT_VERSION = 2;


        internal CL_AnimeEpisode_User _contract = null;

        internal virtual CL_AnimeEpisode_User Contract
        {
            get
            {
                if ((_contract == null) && (ContractBlob != null) && (ContractBlob.Length > 0) && (ContractSize > 0))
                    _contract = CompressionHelper.DeserializeObject<CL_AnimeEpisode_User>(ContractBlob, ContractSize);
                if (_contract != null)
                {
                    SVR_AnimeSeries_User seuse = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(JMMUserID, AnimeSeriesID);
                    _contract.UnwatchedEpCountSeries = seuse?.UnwatchedEpisodeCount ?? 0;
                    SVR_AnimeEpisode aep = RepoFactory.AnimeEpisode.GetByID(AnimeEpisodeID);
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