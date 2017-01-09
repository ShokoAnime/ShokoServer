using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_AnimeGroup_User : AnimeGroup_User
    {
        public int? AnimeGroupParentID { get; set; }
        public int? DefaultAnimeSeriesID { get; set; }

        public string GroupName { get; set; }
        public string Description { get; set; }
        public int IsManuallyNamed { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public string SortName { get; set; }
        public DateTime? EpisodeAddedDate { get; set; }
        public DateTime? LatestEpisodeAirDate { get; set; }

        public int OverrideDescription { get; set; }

        public int MissingEpisodeCount { get; set; }
        public int MissingEpisodeCountGroups { get; set; }



        public DateTime? Stat_AirDate_Min { get; set; }
        public DateTime? Stat_AirDate_Max { get; set; }
        public DateTime? Stat_EndDate { get; set; }
        public DateTime? Stat_SeriesCreatedDate { get; set; }
        public decimal? Stat_UserVotePermanent { get; set; }
        public decimal? Stat_UserVoteTemporary { get; set; }
        public decimal? Stat_UserVoteOverall { get; set; }
        public HashSet<string> Stat_AllTags { get; set; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        public HashSet<int> Stat_AllYears { get; set; } = new HashSet<int>();
        public HashSet<string> Stat_AllCustomTags { get; set; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        public HashSet<string> Stat_AllTitles { get; set; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        public HashSet<string> Stat_AnimeTypes { get; set; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        public bool Stat_IsComplete { get; set; }
        public bool Stat_HasFinishedAiring { get; set; }
        public bool Stat_IsCurrentlyAiring { get; set; }
        public bool Stat_HasTvDBLink { get; set; }
        public bool Stat_HasMALLink { get; set; }
        public bool Stat_HasMovieDBLink { get; set; }
        public bool Stat_HasMovieDBOrTvDBLink { get; set; }
        public HashSet<string> Stat_AllVideoQuality { get; set; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        public HashSet<string> Stat_AllVideoQuality_Episodes { get; set; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        public HashSet<string> Stat_AudioLanguages { get; set; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        public HashSet<string> Stat_SubtitleLanguages { get; set; } = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        public int Stat_SeriesCount { get; set; }
        public int Stat_EpisodeCount { get; set; }
        public decimal Stat_AniDBRating { get; set; }
        public string ServerPosterPath { get; set; }
        public CL_AnimeSeries_User SeriesForNameOverride { get; set; }
    }
}
