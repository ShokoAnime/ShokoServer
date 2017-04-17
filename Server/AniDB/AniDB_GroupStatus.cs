

namespace Shoko.Models.Server
{
    public class AniDB_GroupStatus
    {
        #region DB columns
        public int AniDB_GroupStatusID { get; set; }
        public int AnimeID { get; set; }
        public int GroupID { get; set; }
        public string GroupName { get; set; }
        public int CompletionState { get; set; }
        public int LastEpisodeNumber { get; set; }
        public int Rating { get; set; }
        public int Votes { get; set; }
        public string EpisodeRange { get; set; }
        #endregion

        public AniDB_GroupStatus() //Empty Constructor for nhibernate
        {

        }
    }
}