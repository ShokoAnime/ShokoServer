using AniDBAPI;

namespace JMMServer.Entities
{
    public class AniDB_Tag
    {
        public int AniDB_TagID { get; private set; }
        public int TagID { get; set; }
        public int Spoiler { get; set; }
        public int LocalSpoiler { get; set; }
        public int GlobalSpoiler { get; set; }
        public string TagName { get; set; }
        public int TagCount { get; set; }
        public string TagDescription { get; set; }

        public void Populate(Raw_AniDB_Tag rawTag)
        {
            this.TagID = rawTag.TagID;
            this.GlobalSpoiler = rawTag.GlobalSpoiler;
            this.LocalSpoiler = rawTag.LocalSpoiler;
            this.Spoiler = 0;
            this.TagCount = 0;
            this.TagDescription = rawTag.TagDescription;
            this.TagName = rawTag.TagName;
        }
    }
}