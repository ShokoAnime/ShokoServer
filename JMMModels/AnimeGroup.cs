using System;
using System.Collections.Generic;
using JMMModels.Attributes;
using JMMModels.Childs;

namespace JMMModels
{
    public class AnimeGroup : MissingCount
    {
        public string Id { get; set; }
        public string ParentId { get; set; }
        public string GroupName { get; set; }
        public string Description { get; set; }
        public bool IsManuallyNamed { get; set; }
        public string SortName { get; set; }
        public bool OverrideDescription { get; set; }
        public DateTime? EpisodeAddedDate { get; set; }
        public string DefaultAnimeSeriesId { get; set; }
        public List<Anime_Custom_Tag> CustomTags { get; set; }

 

        //Contract Level
        [Level(1, InheritedLevel = true)]
        public List<AnimeSerie> AnimeSeries { get; set; }
        [Level(1, InheritedLevel = true)]
        public List<AnimeSerie> SubGroups { get; set; }


        //Calculated Stats
        public List<GroupUserStats> UsersStats { get; set; }

        [Level(0, TillLevel = 0, InheritedLevel = true)]
        public HashSet<string> AnimeSerieIDs { get; set; }
        [Level(0, TillLevel = 0, InheritedLevel = true)]
        public HashSet<string> SubGroupsIDs { get; set; }


        public bool HasTvDB { get; set; }
        public bool HasMAL { get; set; }
        public bool HasTrakt { get; set; }
        public bool HasMovieDB { get; set; }
        public List<AniDB_Anime_Tag> Tags { get; set; } 
        public bool HasCompletedSeries { get; set; }
        public bool IsCompletedGroup { get; set; }
        public bool HasSeriesFinishingAiring { get; set; }
        public bool IsGroupFinishingAiring { get; set; }
        public bool HasSeriesCurrentlyAiring { get; set; }
        public DateTime? FirstSerieAirDate { get; set; }
        public DateTime? LastSerieAirDate { get; set; }
        public DateTime? FirstSerieCreationDate { get; set; }
        public DateTime? LastSerieEndDate { get; set; }
        public int SeriesCount { get; set; }
        public int NormalEpisodeCount { get; set; }
        public int SpecialEpisodeCount { get; set; }
        public float SumSeriesRating { get; set; }
        public float SumSeriesTempRating { get; set; }
        public int CountSeriesRating { get; set; }
        public HashSet<AniDB_Type> AniDB_Types { get; set; }
        public HashSet<string> AvailableVideoQualities { get; set; }
        public HashSet<string> AvailableReleaseQualities { get; set; } 
        public HashSet<string> Languages { get; set; }
        public HashSet<string> Subtitles { get; set; }


        public List<IImageInfo> Fanarts { get; set; }
        public List<IImageInfo> Posters { get; set; }
        public List<IImageInfo> Banners { get; set; }

    }
}
