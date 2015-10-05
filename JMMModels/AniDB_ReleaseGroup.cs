using System;
using System.Collections.Generic;
using JMMModels.Childs;

namespace JMMModels
{
    public class AniDB_ReleaseGroup : ImageInfo
    {
        public string Id { get; set; }
        public float Rating { get; set; }
        public int Votes { get; set; }
        public int AnimeCount { get; set; }
        public int FileCount { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string IRCChannel { get; set; }
        public string IRCServer { get; set; }
        public string URL { get; set; }
        public string PicName { get; set; }
        public AniDB_Date FoundedDate { get; set; }
        public AniDB_Date DisbandedDate { get; set; }
        public DateTime? LastRelease { get; set; }
        public DateTime? LastActivityDate { get; set; }
        public List<AniDB_ReleaseGroup_Relation> Relations { get; set; }

        public List<AniDB_Vote> MyVotes { get; set; } 
    }
}
