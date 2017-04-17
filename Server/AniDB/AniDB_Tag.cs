

namespace Shoko.Models.Server
{
    public class AniDB_Tag
    {
        public int AniDB_TagID { get; set; }
        public int TagID { get; set; }
        public int Spoiler { get; set; }
        public int LocalSpoiler { get; set; }
        public int GlobalSpoiler { get; set; }
        public string TagName { get; set; }
        public int TagCount { get; set; }
        public string TagDescription { get; set; }

        public AniDB_Tag()
        {
        }
    }
}