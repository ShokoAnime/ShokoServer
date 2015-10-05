namespace JMMModels.Childs
{
    public class AniDB_Anime_ReleaseGroup
    {
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        public AniDB_Completition_State CompletitionState { get; set; }
        public int LastEpisodeNumber { get; set; }
        public float Rating { get; set; }
        public int Votes { get; set; }
        public string EpisodeRange { get; set; }

    }
}
