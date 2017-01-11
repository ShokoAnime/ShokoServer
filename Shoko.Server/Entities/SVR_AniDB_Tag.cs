using AniDBAPI;
using Shoko.Models.Server;

namespace Shoko.Server.Entities
{
    public class SVR_AniDB_Tag : AniDB_Tag
    {
        public SVR_AniDB_Tag()
        {
        }
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