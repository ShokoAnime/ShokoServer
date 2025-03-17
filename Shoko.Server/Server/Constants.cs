namespace Shoko.Server.Server;

public static class Constants
{
    public const string SentryDsn = "SENTRY_DSN_KEY_GOES_HERE";

    public static readonly string AniDBTitlesURL = @"http://anidb.net/api/anime-titles.xml.gz";

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
        MySQL = 2,
    }

    // http://wiki.anidb.net/w/WebAOM#Move.2Frename_system
    public struct FileRenameTag
    {
        public static readonly string AnimeNameMain = "%ann";
        public static readonly string AnimeNameKanji = "%kan";
        public static readonly string AnimeNameEnglish = "%eng";
        public static readonly string EpisodeNameRomaji = "%epn";
        public static readonly string EpisodeNameEnglish = "%epr";
        public static readonly string EpisodeNumber = "%enr";
        public static readonly string GroupShortName = "%grp";
        public static readonly string GroupLongName = "%grl";
        public static readonly string ED2KLower = "%ed2";
        public static readonly string ED2KUpper = "%ED2";
        public static readonly string CRCLower = "%crc";
        public static readonly string CRCUpper = "%CRC";
        public static readonly string FileVersion = "%ver";
        public static readonly string Source = "%src";
        public static readonly string Resolution = "%res";
        public static readonly string VideoHeight = "%vdh";
        public static readonly string Year = "%yea";
        public static readonly string Episodes = "%eps"; // Total number of episodes
        public static readonly string Type = "%typ"; // Type [unknown, TV, OVA, Movie, TV Special, Other, web]
        public static readonly string FileID = "%fid";
        public static readonly string AnimeID = "%aid";
        public static readonly string EpisodeID = "%eid";
        public static readonly string GroupID = "%gid";
        public static readonly string DubLanguage = "%dub";
        public static readonly string SubLanguage = "%sub";
        public static readonly string VideoCodec = "%vid"; //tracks separated with '
        public static readonly string AudioCodec = "%aud"; //tracks separated with '
        public static readonly string VideoBitDepth = "%bit"; // 8bit, 10bit

        public static readonly string OriginalFileName = "%sna";
        // The original file name as specified by the sub group

        public static readonly string Censored = "%cen";
        public static readonly string Deprecated = "%dep";


        /*
        %md5 / %MD5	 md5 sum (lower/upper)
        %sha / %SHA	 sha1 sum (lower/upper)
        %inv	 Invalid crc string
         * */
    }

    public struct FileRenameReserved
    {
        public static readonly string Do = "DO";
        public static readonly string Fail = "FAIL";
        public static readonly string Add = "ADD";
        public static readonly string Replace = "REPLACE";
        public static readonly string None = "none"; // used for videos with no audio or no subitle languages
        public static readonly string Unknown = "unknown"; // used for videos with no audio or no subitle languages
    }

    public struct URLS
    {
        public const string MAL_Series = @"https://myanimelist.net/anime/{0}";

        public const string AniDB_Series = @"https://anidb.net/perl-bin/animedb.pl?show=anime&aid={0}";

        public const string AniDB_SeriesDiscussion =
            @"https://anidb.net/perl-bin/animedb.pl?show=threads&do=anime&id={0}";

        public const string AniDB_Images = @"https://{0}/images/main/{{0}}";

        // This is the fallback if the API response does not work.
        public const string AniDB_Images_Domain = @"cdn.anidb.net";

        public const string Trakt_Series = @"https://trakt.tv/show/{0}";
    }

    public struct TMDB
    {
        // For local development, please replace the text below with your TMDB API key, or insert the key in your settings.
        public const string ApiKey = "TMDB_API_KEY_GOES_HERE";
    }
}
