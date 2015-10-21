using System;
using System.Collections.Generic;

namespace JMMModels.Childs
{
    public class AniDB_Anime : ImageInfoWithDateUpdate
    {
        public string Id { get; set; } 
        public int EpisodeCount { get; set; }
        public AniDB_Date AirDate { get; set; }
        public AniDB_Date EndDate { get; set; }
        public string Url { get; set; }
        public string Picname { get; set; }
        public int BeginYear { get; set; }
        public int EndYear { get; set; }
        public AniDB_Type AnimeType { get; set; }
        public string MainTitle { get; set; }
        public string AllTitles { get; set; }
        public string AllCategories { get; set; }
        public string AllTags { get; set; }
        public string Description { get; set; }
        public int EpisodeCountNormal { get; set; }
        public int EpisodeCountSpecial { get; set; }
        public float Rating { get; set; }
        public int VoteCount { get; set; }
        public float TempRating { get; set; }
        public int TempVoteCount { get; set; }
        public float AvgReviewRating { get; set; }
        public int ReviewCount { get; set; }
        public DateTime DateTimeDescUpdated { get; set; }


        public string AwardList { get; set; }
        public bool Restricted { get; set; }
        public int? AnimePlanetId { get; set; }
        public int? ANNId { get; set; }
        public int? AllCinemaId { get; set; }
        public int? AnimeNfo { get; set; }
        public int? LatestEpisodeNumber { get; set; }
        public LinkFlags DisableExternalLinksFlag { get; set; }


        //Account Related
        public List<AniDB_Vote> MyVotes { get; set; }

        //Collections
        public List<AniDB_Creator> MainCreators { get; set; }
        public List<AniDB_Creator> Creators { get; set; }
        public List<AniDB_Anime_Character> Characters { get; set; }  
        public List<AniDB_Anime_Relation> Relations { get; set; } 
        public List<AniDB_Anime_Review> Reviews { get; set; }
        public List<AniDB_Anime_Similar> Similars { get; set; }
        public List<AniDB_Anime_Tag> Tags { get; set; }
        public List<AniDB_Anime_Title> Titles { get; set; }   
        public List<AniDB_Anime_ReleaseGroup> ReleaseGroups { get; set; } 
        public List<AniDB_Anime_MovieDB> MovieDBs { get; set; }
        public List<AniDB_Anime_MAL> MALs { get; set; }
        public List<AniDB_Anime_TvDB> TvDBs { get; set; }
        public List<AniDB_Anime_Trakt> Trakts { get; set; }

        //Images
        public List<MovieDB_Image> MovieDBFanarts { get; set; }
        public List<MovieDB_Image> MovieDBPosters { get; set; }
        public List<TvDB_Image> TvDBFanarts { get; set; }
        public List<TvDB_Image> TvDBPosters { get; set; }
        public List<TvDB_Image> TvDBBanners { get; set; }
        public List<Trakt_Image> TraktPosters { get; set; }
        public List<Trakt_Image> TraktFanarts{ get; set; }

    }
    [Flags]
    public enum LinkFlags
    {
        LinkTvDB = 1,
        LinkTrakt = 2,
        LinkMAL = 4,
        LinkMovieDB = 8
    }
}
