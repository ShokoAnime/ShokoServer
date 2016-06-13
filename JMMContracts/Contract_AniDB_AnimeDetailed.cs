using System.Collections.Generic;

namespace JMMContracts
{
    public class Contract_AniDB_AnimeDetailed
    {
        public Contract_AniDBAnime AniDBAnime { get; set; }
        public List<Contract_AnimeTitle> AnimeTitles { get; set; }
        public List<Contract_AnimeTag> Tags { get; set; }
        public List<Contract_CustomTag> CustomTags { get; set; }
        public Contract_AniDBVote UserVote { get; set; }

        public HashSet<string> Stat_AllVideoQuality { get; set; }
        public HashSet<string> Stat_AllVideoQuality_Episodes { get; set; }
        public HashSet<string> Stat_AudioLanguages { get; set; }
        public HashSet<string> Stat_SubtitleLanguages { get; set; }
    }
}