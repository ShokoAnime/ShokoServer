using System;
using System.Collections.Generic;
using JMMModels.Attributes;
using JMMModels.Childs;

namespace JMMModels
{
    public partial class AnimeSerie : MissingCount
    {
        public string Id { get; set; }
        public string GroupId { get; set; }
        public string DefaultAudioLanguage { get; set; }
        public string DefaultSubtitleLanguage { get; set; }
        public DateTime? EpisodeAddedDate { get; set; }
        public int LatestLocalEpisodeNumber { get; set; }
        public string SeriesNameOverride { get; set; }
        public bool IsMovie { get; set; }

        public List<Anime_Custom_Tag> CustomTags { get; set; } 
        [Level(1)]
        public List<AnimeEpisode> Episodes { get; set; }
        public List<ExtendedUserStats> UsersStats { get; set; }
        public List<RecomendationIgnore> IgnoreRecommendations { get; set; }
        public AniDB_Anime AniDB_Anime { get; set; }


        //Calculated
        public HashSet<string> AvailableVideoQualities { get; set; }
        public HashSet<string> AvailableReleaseQualities { get; set; }
        public HashSet<string> Languages { get; set; }
        public HashSet<string> Subtitles { get; set; }


    }
}
