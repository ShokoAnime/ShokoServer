using JMMContracts;
using NLog;

namespace JMMServer.Entities
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
            var contract = new Contract_FileFfdshowPreset();
            contract.FileFfdshowPresetID = FileFfdshowPresetID;
            contract.Hash = Hash;
            contract.FileSize = FileSize;
            contract.Preset = Preset;

            return contract;
        }
    }
}