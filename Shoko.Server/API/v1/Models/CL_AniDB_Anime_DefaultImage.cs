using System;

namespace Shoko.Server.API.v1.Models;

public class CL_AniDB_Anime_DefaultImage : ICloneable
{
    public int AniDB_Anime_DefaultImageID { get; set; }
    public int AnimeID { get; set; }
    public int ImageParentID { get; set; }
    public int ImageParentType { get; set; }
    public int ImageType { get; set; }

    public CL_MovieDB_Poster MoviePoster { get; set; }
    public CL_MovieDB_Fanart MovieFanart { get; set; }

    public object TVPoster { get; set; }
    public object TVFanart { get; set; }
    public object TVWideBanner { get; set; }

    public CL_AniDB_Anime_DefaultImage()
    {
    }

    public object Clone()
    {
        var image = new CL_AniDB_Anime_DefaultImage()
        {
            AniDB_Anime_DefaultImageID = AniDB_Anime_DefaultImageID,
            AnimeID = AnimeID,
            ImageParentID = ImageParentID,
            ImageParentType = ImageParentType,
            ImageType = ImageType,
            MoviePoster = (CL_MovieDB_Poster)MoviePoster?.Clone(),
            MovieFanart = (CL_MovieDB_Fanart)MovieFanart?.Clone(),
            TVPoster = TVPoster,
            TVFanart = TVFanart,
            TVWideBanner = TVWideBanner,
        };

        return image;
    }
}
