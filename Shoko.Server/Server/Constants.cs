namespace Shoko.Server.Server;

public static class Constants
{
    public const string SentryDsn = "SENTRY_DSN_KEY_GOES_HERE";

    public static readonly string AnidbHttpApiUrl = @"http://api.anidb.net:9001";

    public static readonly string AnidbCdnUrl = @"https://cdn.anidb.net";

    public static readonly string AnidbTitleCacheUrl = @"https://anidb.net/api/anime-titles.xml.gz";

    public const string DatabaseTypeKey = "Database";

    public static readonly string NO_GROUP_INFO = "NO GROUP INFO";

    public struct GroupFilterName
    {
        public const string All = "All";
        public const string ContinueWatching = "Continue Watching";
        public const string Favorites = "Favorites";
        public const string MissingEpisodes = "Missing Episodes";
        public const string NewlyAddedSeries = "Newly Added Series";
        public const string NewlyAiringSeries = "Newly Airing Series";
        public const string MissingVotes = "Missing Votes";
        public const string MissingLinks = "Missing Links";
        public const string RecentlyWatched = "Recently Watched";
    }

    public enum DatabaseType
    {
        SQLite = 0,
        SQLServer = 1,
        MSSQL = SQLServer,
        MySQL = 2,
        MariaDB = MySQL,
    }

    public struct URLS
    {
        public const string AniDB_Images = @"{0}/images/main/{{0}}";
    }
}
