using System;

namespace Shoko.Models.Server
{
    public class CL_AniDB_Seiyuu : ICloneable
    {
        public int AniDB_SeiyuuID { get; set; }
        public int SeiyuuID { get; set; }
        public string SeiyuuName { get; set; }
        public string PicName { get; set; }

        public object Clone()
        {
            return new CL_AniDB_Seiyuu
            {
                AniDB_SeiyuuID = AniDB_SeiyuuID,
                SeiyuuID = SeiyuuID,
                SeiyuuName = SeiyuuName,
                PicName = PicName
            };
        }
    }
}
