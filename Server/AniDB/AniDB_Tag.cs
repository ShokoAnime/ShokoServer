using System;

namespace Shoko.Models.Server
{
    public class AniDB_Tag
    {
        /// <summary>
        /// Local anidb tag id.
        /// </summary>
        public int AniDB_TagID { get; set; }

        /// <summary>
        /// Universal anidb tag id.
        /// </summary>
        public int TagID { get; set; }

        /// <summary>
        /// Universal anidb tag id of the parent tag, if any.
        /// </summary>
        /// <value>An int if the tag has a parent, otherwise null.</value>
        public int? ParentTagID { get; set; }

        /// <summary>
        /// The tag name to use.
        /// </summary>
        public string TagName { get => TagNameOverride ?? TagNameSource; }

        /// <summary>
        /// The original tag name as shown on anidb.
        /// </summary>
        public string TagNameSource { get; set; }

        /// <summary>
        /// Name override for those tags where the original name doesn't make
        /// sense or is otherwise confusing.
        /// </summary>
        public string TagNameOverride { get; set; }

        /// <summary>
        /// True if this tag itself is considered as a spoiler, regardless of
        /// which anime it's attached to.
        /// </summary>
        public bool GlobalSpoiler { get; set; }

        /// <summary>
        /// True if the tag has been verified for use by a mod. Unverified tags
        /// are not shown in AniDB's UI except when editing tags.
        /// </summary>
        public bool Verified { get; set; }

        /// <summary>
        /// The description for the tag, if any.
        /// </summary>
        public string TagDescription { get; set; }

        /// <summary>
        /// The date (with no time) the tag was last updated at.
        /// </summary>
        public DateTime LastUpdated { get; set; }

        public AniDB_Tag()
        {
        }
    }
}
