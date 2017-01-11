using System.IO;
using Shoko.Models.Server;
using Shoko.Server.ImageDownload;

namespace Shoko.Server.Entities
{
    public class SVR_AniDB_Seiyuu : AniDB_Seiyuu
    {
        public SVR_AniDB_Seiyuu()
        {
        }
        public string PosterPath
        {
            get
            {
                if (string.IsNullOrEmpty(PicName)) return "";

                return Path.Combine(ImageUtils.GetAniDBCreatorImagePath(SeiyuuID), PicName);
            }
        }


    }
}