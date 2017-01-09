using System.IO;
using Shoko.Models;


namespace Shoko.Models.Server
{
    public class AniDB_Seiyuu
    {
        public int AniDB_SeiyuuID { get; private set; }
        public int SeiyuuID { get; set; }
        public string SeiyuuName { get; set; }
        public string PicName { get; set; }
    }
}