using System.Collections.Generic;

namespace JMMModels.Childs
{
    public class AnimeEpisode : DateUpdate
    {
        public string Id { get; set; } //GUID
        public List<UserStats> UsersStats { get; set; }    
        public Dictionary<int, List<AniDB_Episode>> AniDbEpisodes { get; set; }  //first_anidb, more than one anidb_episode if multipart
        public Episode_TvDBEpisode TvDBEpisode { get; set; }
        public Episode_TraktEpisode TraktEpisode { get; set; }
    }
}
