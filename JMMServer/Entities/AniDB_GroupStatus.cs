using AniDBAPI;

namespace JMMServer.Entities
{
    public class AniDB_GroupStatus
    {
        public AniDB_GroupStatus()
        {
        }

        public AniDB_GroupStatus(Raw_AniDB_GroupStatus raw)
        {
            AnimeID = raw.AnimeID;
            GroupID = raw.GroupID;
            GroupName = raw.GroupName;
            CompletionState = raw.CompletionState;
            LastEpisodeNumber = raw.LastEpisodeNumber;
            Rating = raw.Rating;
            Votes = raw.Votes;
            EpisodeRange = raw.EpisodeRange;
        }

        public int AniDB_GroupStatusID { get; private set; }
        public int AnimeID { get; set; }
        public int GroupID { get; set; }
        public string GroupName { get; set; }
        public int CompletionState { get; set; }
        public int LastEpisodeNumber { get; set; }
        public int Rating { get; set; }
        public int Votes { get; set; }
        public string EpisodeRange { get; set; }

        /// <summary>
        ///     looking at the episode range determine if the group has released a file
        ///     for the specified episode number
        /// </summary>
        /// <param name="episodeNumber"></param>
        /// <returns></returns>
        public bool HasGroupReleasedEpisode(int episodeNumber)
        {
            // examples
            // 1-12
            // 1
            // 5-10
            // 1-10, 12

            var ranges = EpisodeRange.Split(',');

            foreach (var range in ranges)
            {
                var subRanges = range.Split('-');
                if (subRanges.Length == 1) // 1 episode
                {
                    if (int.Parse(subRanges[0]) == episodeNumber) return true;
                }
                if (subRanges.Length == 2) // range
                {
                    if (episodeNumber >= int.Parse(subRanges[0]) && episodeNumber <= int.Parse(subRanges[1]))
                        return true;
                }
            }

            return false;
        }

        public override string ToString()
        {
            return string.Format("Group Status for anime: {0} - {1} : {2}", AnimeID, GroupName, EpisodeRange);
        }
    }
}