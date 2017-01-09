using System.IO;
using JMMServer.ImageDownload;
using Shoko.Models.Server;

namespace JMMServer.Entities
{
    public class SVR_AniDB_Seiyuu : AniDB_Seiyuu
    {

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