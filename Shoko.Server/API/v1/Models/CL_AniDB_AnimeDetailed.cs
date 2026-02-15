using System.Collections.Generic;

namespace Shoko.Server.API.v1.Models;

public class CL_AniDB_AnimeDetailed
{
    public CL_AniDB_Anime AniDBAnime { get; set; }
    public List<CL_AnimeTitle> AnimeTitles { get; set; }
    public List<CL_AnimeTag> Tags { get; set; }
    public List<CL_CustomTag> CustomTags { get; set; }
    public CL_AniDB_Vote UserVote { get; set; }

    public SortedSet<string> Stat_AllSeasons { get; set; } = new SortedSet<string>(new CL_SeasonComparator());
    public HashSet<string> Stat_AllVideoQuality { get; set; }
    public HashSet<string> Stat_AllVideoQuality_Episodes { get; set; }
    public HashSet<string> Stat_AudioLanguages { get; set; }
    public HashSet<string> Stat_SubtitleLanguages { get; set; }
}
