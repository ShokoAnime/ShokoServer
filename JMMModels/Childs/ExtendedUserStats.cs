using System.Collections.Generic;

namespace JMMModels.Childs
{
    public class ExtendedUserStats : UserStats
    {
        public bool IsFave { get; set; }
        public int UnwatchedEpisodeCount { get; set; }
        public int WatchedEpisodeCount { get; set; }

    }

    public class GroupUserStats : ExtendedUserStats
    {
        public HashSet<string> GroupFilters { get; set; }

        //Calculated
        public float? UserVotesPermanent { get; set; }
        public float? UserVotesTemporary { get; set; }
        public float? UserVotes { get; set; }
        public bool HasVotes { get; set; }


    }
}
