using System;
using System.Collections.Generic;

namespace Shoko.Models
{
    public class CL_AniDB_Anime
    {
        public int AnimeID { get; set; }
        public int EpisodeCount { get; set; }
        public DateTime? AirDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string URL { get; set; }
        public string Picname { get; set; }
        public int BeginYear { get; set; }
        public int EndYear { get; set; }
        public int AnimeType { get; set; }
        public string MainTitle { get; set; }
        public string FormattedTitle { get; set; }
        public HashSet<string> AllTitles { get; set; }
        public HashSet<string> AllTags { get; set; }
        public string Description { get; set; }
        public int EpisodeCountNormal { get; set; }
        public int EpisodeCountSpecial { get; set; }
        public int Rating { get; set; }
        public int VoteCount { get; set; }
        public int TempRating { get; set; }
        public int TempVoteCount { get; set; }
        public int AvgReviewRating { get; set; }
        public int ReviewCount { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public DateTime DateTimeDescUpdated { get; set; }
        public int ImageEnabled { get; set; }
        public string AwardList { get; set; }
        public int Restricted { get; set; }
        public int? AnimePlanetID { get; set; }
        public int? ANNID { get; set; }
        public int? AllCinemaID { get; set; }
        public int? AnimeNfo { get; set; }
        public int? LatestEpisodeNumber { get; set; }
        public int DisableExternalLinksFlag { get; set; }

        public Contract_AniDB_Anime_DefaultImage DefaultImagePoster { get; set; }
        public Contract_AniDB_Anime_DefaultImage DefaultImageFanart { get; set; }
        public Contract_AniDB_Anime_DefaultImage DefaultImageWideBanner { get; set; }
        public List<Contract_AniDB_Character> Characters { get; set; }
	    public List<Contract_AniDB_Anime_DefaultImage> Fanarts { get; set; }
	    public List<Contract_AniDB_Anime_DefaultImage> Banners { get; set; }
    }
}