using System;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_AniDB_Anime_DefaultImage : AniDB_Anime_DefaultImage, ICloneable
    {
        public MovieDB_Poster MoviePoster { get; set; }
        public MovieDB_Fanart MovieFanart { get; set; }

        public TvDB_ImagePoster TVPoster { get; set; }
        public TvDB_ImageFanart TVFanart { get; set; }
        public TvDB_ImageWideBanner TVWideBanner { get; set; }

        public CL_AniDB_Anime_DefaultImage(AniDB_Anime_DefaultImage obj)
        {
            AniDB_Anime_DefaultImageID = obj.AniDB_Anime_DefaultImageID;
            AnimeID = obj.AnimeID;
            ImageParentID = obj.ImageParentID;
            ImageParentType = obj.ImageParentType;
            ImageType = obj.ImageType;
        }

        public CL_AniDB_Anime_DefaultImage()
        {
        }

        public new object Clone()
        {
            var image = new CL_AniDB_Anime_DefaultImage(this)
            {
                MoviePoster = (MovieDB_Poster) MoviePoster?.Clone(),
                MovieFanart = (MovieDB_Fanart) MovieFanart?.Clone(),
                TVPoster = (TvDB_ImagePoster) TVPoster?.Clone(),
                TVFanart = (TvDB_ImageFanart) TVFanart?.Clone(),
                TVWideBanner = (TvDB_ImageWideBanner) TVWideBanner?.Clone()
            };

            return image;
        }
    }
}
