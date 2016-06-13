namespace JMMServer.Providers.Azure
{
    public class CrossRef_File_EpisodeInput
    {
        public string Hash { get; set; }
        public int AnimeID { get; set; }
        public int EpisodeID { get; set; }
        public int Percentage { get; set; }
        public int EpisodeOrder { get; set; }
        public string Username { get; set; }

        public CrossRef_File_EpisodeInput()
        {
        }

        public CrossRef_File_EpisodeInput(JMMServer.Entities.CrossRef_File_Episode xref)
        {
            this.Hash = xref.Hash;
            this.AnimeID = xref.AnimeID;
            this.EpisodeID = xref.EpisodeID;
            this.Percentage = xref.Percentage;
            this.EpisodeOrder = xref.EpisodeOrder;

            this.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                this.Username = Constants.AnonWebCacheUsername;
        }
    }
}