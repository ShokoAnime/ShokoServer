using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_AniDB_AnimeDetailed : ICloneable
    {
        public CL_AniDB_Anime AniDBAnime { get; set; }
        public List<CL_AnimeTitle> AnimeTitles { get; set; }
        public List<CL_AnimeTag> Tags { get; set; }
        public List<CustomTag> CustomTags { get; set; }
        public AniDB_Vote UserVote { get; set; }

        public SortedSet<string> Stat_AllSeasons { get; set; } = new SortedSet<string>(new SeasonComparator());
        public HashSet<string> Stat_AllVideoQuality { get; set; }
        public HashSet<string> Stat_AllVideoQuality_Episodes { get; set; }
        public HashSet<string> Stat_AudioLanguages { get; set; }
        public HashSet<string> Stat_SubtitleLanguages { get; set; }
        public object Clone()
        {
            return new CL_AniDB_AnimeDetailed
            {
                AniDBAnime = (CL_AniDB_Anime) AniDBAnime?.Clone(),
                AnimeTitles = AnimeTitles?.Select(a => a.Clone()).Cast<CL_AnimeTitle>().ToList(),
                Tags = Tags?.Select(a => a.Clone()).Cast<CL_AnimeTag>().ToList(),
                CustomTags = CustomTags?.Select(a => a.Clone()).Cast<CustomTag>().ToList(),
                UserVote = UserVote,
                Stat_AllSeasons = Stat_AllSeasons == null ? null : new SortedSet<string>(Stat_AllSeasons, Stat_AllSeasons.Comparer),
                Stat_AllVideoQuality = Stat_AllVideoQuality == null ? null : new HashSet<string>(Stat_AllVideoQuality, Stat_AllVideoQuality.Comparer),
                Stat_AllVideoQuality_Episodes = Stat_AllVideoQuality_Episodes == null ? null : new HashSet<string>(Stat_AllVideoQuality_Episodes, Stat_AllVideoQuality_Episodes.Comparer),
                Stat_AudioLanguages = Stat_AudioLanguages == null ? null : new HashSet<string>(Stat_AudioLanguages, Stat_AudioLanguages.Comparer),
                Stat_SubtitleLanguages = Stat_SubtitleLanguages == null ? null : new HashSet<string>(Stat_SubtitleLanguages, Stat_SubtitleLanguages.Comparer)
            };
        }
    }
}
