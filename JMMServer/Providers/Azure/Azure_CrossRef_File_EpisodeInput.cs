namespace JMMServer.Providers.Azure
{
    public class CrossRef_File_EpisodeInput
    {
        public CrossRef_File_EpisodeInput()
        {
        }

        public CrossRef_File_EpisodeInput(Entities.CrossRef_File_Episode xref)
        {
            Hash = xref.Hash;
            AnimeID = xref.AnimeID;
            EpisodeID = xref.EpisodeID;
            Percentage = xref.Percentage;
            EpisodeOrder = xref.EpisodeOrder;

            Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                Username = Constants.AnonWebCacheUsername;
        }

        public string Hash { get; set; }
        public int AnimeID { get; set; }
        public int EpisodeID { get; set; }
        public int Percentage { get; set; }
        public int EpisodeOrder { get; set; }
        public string Username { get; set; }
    }
}