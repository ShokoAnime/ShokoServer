using NLog;
using Shoko.Models;

namespace Shoko.Server.Entities
{
    public class FileFfdshowPreset
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int FileFfdshowPresetID { get; private set; }
        public string Hash { get; set; }
        public long FileSize { get; set; }
        public string Preset { get; set; }

        public Contract_FileFfdshowPreset ToContract()
        {
            Contract_FileFfdshowPreset contract = new Contract_FileFfdshowPreset();
            contract.FileFfdshowPresetID = this.FileFfdshowPresetID;
            contract.Hash = this.Hash;
            contract.FileSize = this.FileSize;
            contract.Preset = this.Preset;

            return contract;
        }
    }
}