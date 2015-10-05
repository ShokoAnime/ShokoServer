using System.Collections.Generic;
using JMMModels.Childs;

namespace JMMModels
{
    public class VideoLocal : MediaInfo
    {
        public string Id { get; set; }
        public FileInfo FileInfo { get; set; }
        public string CRC { get; set; }
        public string MD5 { get; set; }
        public string SHA1 { get; set; }
        public CrossRefSourceType CrossRefSource { get; set; }
        public HashSource HashSource { get; set; }
        public bool IsIgnored { get; set; }
        public bool IsVariation { get; set; }

        public HashSet<string> FFdShowPreset { get; set; }
        public List<UserStats> UsersStats { get; set; }

    }
}
