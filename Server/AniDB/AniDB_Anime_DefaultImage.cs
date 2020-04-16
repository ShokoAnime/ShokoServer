using System;

namespace Shoko.Models.Server
{
    public class AniDB_Anime_DefaultImage : ICloneable
    {
        #region Server DB columns

        public int AniDB_Anime_DefaultImageID { get; set; }
        public int AnimeID { get; set; }
        public int ImageParentID { get; set; }
        public int ImageParentType { get; set; }
        public int ImageType { get; set; }

        #endregion
        public AniDB_Anime_DefaultImage() //Empty Constructor for nhibernate
        {

        }

        public object Clone()
        {
            return new AniDB_Anime_DefaultImage
            {
                AniDB_Anime_DefaultImageID = AniDB_Anime_DefaultImageID,
                AnimeID = AnimeID,
                ImageParentID = ImageParentID,
                ImageParentType = ImageParentType,
                ImageType = ImageType
            };
        }
    }
}
