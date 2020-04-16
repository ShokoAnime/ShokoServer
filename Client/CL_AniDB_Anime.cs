using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_AniDB_Anime : AniDB_Anime, ICloneable
    {
        public CL_AniDB_Anime_DefaultImage DefaultImagePoster { get; set; }
        public CL_AniDB_Anime_DefaultImage DefaultImageFanart { get; set; }
        public CL_AniDB_Anime_DefaultImage DefaultImageWideBanner { get; set; }
        public List<CL_AniDB_Character> Characters { get; set; }
        public List<CL_AniDB_Anime_DefaultImage> Fanarts { get; set; }
        public List<CL_AniDB_Anime_DefaultImage> Banners { get; set; }
        public string FormattedTitle { get; set; }

        public CL_AniDB_Anime()
        {
        }

        public CL_AniDB_Anime(AniDB_Anime obj)
        {
            AniDB_AnimeID = obj.AniDB_AnimeID;
            AnimeID = obj.AnimeID;
            EpisodeCount = obj.EpisodeCount;
            AirDate = obj.AirDate;
            EndDate = obj.EndDate;
            URL = obj.URL;
            Picname = obj.Picname;
            BeginYear = obj.BeginYear;
            EndYear = obj.EndYear;
            AnimeType = obj.AnimeType;
            MainTitle = obj.MainTitle;
            AllTitles = obj.AllTitles;
            AllTags = obj.AllTags;
            Description = obj.Description;
            EpisodeCountNormal = obj.EpisodeCountNormal;
            EpisodeCountSpecial = obj.EpisodeCountSpecial;
            Rating = obj.Rating;
            VoteCount = obj.VoteCount;
            TempRating = obj.TempRating;
            TempVoteCount = obj.TempVoteCount;
            AvgReviewRating = obj.AvgReviewRating;
            ReviewCount = obj.ReviewCount;
            DateTimeUpdated = obj.DateTimeUpdated;
            DateTimeDescUpdated = obj.DateTimeDescUpdated;
            ImageEnabled = obj.ImageEnabled;
            AwardList = obj.AwardList;
            Restricted = obj.Restricted;
            AnimePlanetID = obj.AnimePlanetID;
            ANNID = obj.ANNID;
            AllCinemaID = obj.AllCinemaID;
            AnimeNfo = obj.AnimeNfo;
            AnisonID = obj.AnisonID;
            SyoboiID = obj.SyoboiID;
            Site_JP = obj.Site_JP;
            Site_EN = obj.Site_EN;
            Wikipedia_ID = obj.Wikipedia_ID;
            WikipediaJP_ID = obj.WikipediaJP_ID;
            CrunchyrollID = obj.CrunchyrollID;
            LatestEpisodeNumber = obj.LatestEpisodeNumber;
            DisableExternalLinksFlag = obj.DisableExternalLinksFlag;
        }

        public new object Clone()
        {
            var anime = new CL_AniDB_Anime(this)
            {
                DefaultImagePoster = (CL_AniDB_Anime_DefaultImage) DefaultImagePoster?.Clone(),
                DefaultImageFanart = (CL_AniDB_Anime_DefaultImage) DefaultImageFanart?.Clone(),
                DefaultImageWideBanner = (CL_AniDB_Anime_DefaultImage) DefaultImageWideBanner?.Clone(),
                Characters = Characters?.Select(a => a.Clone()).Cast<CL_AniDB_Character>().ToList(),
                Fanarts = Fanarts?.Select(a => a.Clone()).Cast<CL_AniDB_Anime_DefaultImage>().ToList(),
                Banners = Banners?.Select(a => a.Clone()).Cast<CL_AniDB_Anime_DefaultImage>().ToList(),
                FormattedTitle = FormattedTitle
            };

            return anime;
        }
    }
}
