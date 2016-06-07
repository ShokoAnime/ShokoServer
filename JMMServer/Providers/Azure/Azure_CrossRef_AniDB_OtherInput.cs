namespace JMMServer.Providers.Azure
{
    public class CrossRef_AniDB_OtherInput
    {
        public CrossRef_AniDB_OtherInput()
        {
        }

        public CrossRef_AniDB_OtherInput(Entities.CrossRef_AniDB_Other xref)
        {
            AnimeID = xref.AnimeID;
            CrossRefID = xref.CrossRefID;
            CrossRefSource = xref.CrossRefSource;
            CrossRefType = xref.CrossRefType;

            Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                Username = Constants.AnonWebCacheUsername;
        }

        public int AnimeID { get; set; }
        public string CrossRefID { get; set; }
        public int CrossRefSource { get; set; }
        public int CrossRefType { get; set; }
        public string Username { get; set; }
    }
}