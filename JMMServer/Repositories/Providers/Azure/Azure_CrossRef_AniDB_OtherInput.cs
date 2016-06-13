namespace JMMServer.Providers.Azure
{
    public class CrossRef_AniDB_OtherInput
    {
        public int AnimeID { get; set; }
        public string CrossRefID { get; set; }
        public int CrossRefSource { get; set; }
        public int CrossRefType { get; set; }
        public string Username { get; set; }

        public CrossRef_AniDB_OtherInput()
        {
        }

        public CrossRef_AniDB_OtherInput(JMMServer.Entities.CrossRef_AniDB_Other xref)
        {
            this.AnimeID = xref.AnimeID;
            this.CrossRefID = xref.CrossRefID;
            this.CrossRefSource = xref.CrossRefSource;
            this.CrossRefType = xref.CrossRefType;

            this.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                this.Username = Constants.AnonWebCacheUsername;
        }
    }
}