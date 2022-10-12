﻿using System;
using Shoko.Commons.Properties;

namespace Shoko.Server.Server;

public static class Constants
{
    public static readonly string WebCacheError = @"<error>No Results</error>";
    public static readonly string AniDBTitlesURL = @"http://anidb.net/api/anime-titles.xml.gz";
    public static readonly string AnonWebCacheUsername = @"AnonymousWebCacheUser";

    public const string DatabaseTypeKey = "Database";

    public static readonly int ForceLogoutPeriod = 300;
    public static readonly int PingFrequency = 45;

    public static readonly TimeSpan ContractLifespan = TimeSpan.FromHours(2);


    public static readonly string NO_GROUP_INFO = "NO GROUP INFO";
    public static readonly string NO_SOURCE_INFO = "NO SOURCE INFO";

    public struct GroupFilterName
    {
        public static readonly string ContinueWatching = Resources.Filter_Continue;
    }

    public struct DatabaseType
    {
        public const string SqlServer = "SQLServer";
        public const string Sqlite = "SQLite";
        public const string MySQL = "MySQL";
    }

    public struct DBLogType
    {
        public static readonly string APIAniDBHTTP = "AniDB HTTP";
        public static readonly string APIAniDBUDP = "AniDB UDP";
        public static readonly string APIAzureHTTP = "Cache HTTP";
    }

    #region Labels

    // http://wiki.anidb.net/w/WebAOM#Move.2Frename_system
    public struct FileRenameTag
    {
        public static readonly string AnimeNameRomaji = "%ann";
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

    public struct Labels
    {
        public static readonly string LASTWATCHED = "Last Watched";
        public static readonly string NEWEPISODES = "New Episodes";
        public static readonly string FAVES = "Favorites";
        public static readonly string FAVESNEW = "New in Favorites";
        public static readonly string MISSING = "Missing Episodes";
        public static readonly string MAINVIEW = "[ Main View ]";
        public static readonly string PREVIOUSFOLDER = "..";
    }

    public struct SeriesDisplayString
    {
        public static readonly string SeriesName = "<SeriesName>";
        public static readonly string AniDBNameRomaji = "<AniDBNameRomaji>";
        public static readonly string AniDBNameEnglish = "<AniDBNameEnglish>";
        public static readonly string TvDBSeason = "<TvDBSeason>";
        public static readonly string AnimeYear = "<AnimeYear>";
    }

    public struct GroupDisplayString
    {
        public static readonly string GroupName = "<GroupName>";
        public static readonly string AniDBNameRomaji = "<AniDBNameRomaji>";
        public static readonly string AniDBNameEnglish = "<AniDBNameEnglish>";
        public static readonly string AnimeYear = "<AnimeYear>";
    }

    public struct FileSelectionDisplayString
    {
        public static readonly string Group = "<AnGroup>";
        public static readonly string GroupShort = "<AnGroupShort>";
        public static readonly string FileSource = "<FileSource>";
        public static readonly string FileRes = "<FileRes>";
        public static readonly string FileCodec = "<FileCodec>";
        public static readonly string AudioCodec = "<AudioCodec>";
    }

    public struct EpisodeDisplayString
    {
        public static readonly string EpisodeNumber = "<EpNo>";
        public static readonly string EpisodeName = "<EpName>";
    }

    public struct URLS
    {
        public static readonly string MAL_Series_Prefix = @"https://myanimelist.net/anime/";
        public static readonly string MAL_Series = @"https://myanimelist.net/anime/{0}";
        public static readonly string MAL_SeriesDiscussion = @"https://myanimelist.net/anime/{0}/{1}/forum";

        public static readonly string AniDB_File = @"https://anidb.net/perl-bin/animedb.pl?show=file&fid={0}";
        public static readonly string AniDB_Episode = @"https://anidb.net/perl-bin/animedb.pl?show=ep&eid={0}";
        public static readonly string AniDB_Series = @"https://anidb.net/perl-bin/animedb.pl?show=anime&aid={0}";

        public static readonly string AniDB_SeriesDiscussion =
            @"https://anidb.net/perl-bin/animedb.pl?show=threads&do=anime&id={0}";

        public static readonly string AniDB_ReleaseGroup =
            @"https://anidb.net/perl-bin/animedb.pl?show=group&gid={0}";

        public static readonly string AniDB_Images = @"https://{0}/images/main/{{0}}";

        // This is the fallback if the API response does not work.
        public static readonly string AniDB_Images_Domain = @"cdn.anidb.net";

        public static readonly string TvDB_Series = @"https://thetvdb.com/?tab=series&id={0}";

        //public static readonly string tvDBEpisodeURLPrefix = @"http://anidb.net/perl-bin/animedb.pl?show=ep&eid={0}";
        public static readonly string TvDB_Images = @"https://artworks.thetvdb.com/banners/{0}";
        public static readonly string TvDB_Episode_Images = @"https://thetvdb.com/banners/{0}";

        public static readonly string MovieDB_Series = @"https://www.themoviedb.org/movie/{0}";
        public static readonly string Trakt_Series = @"https://trakt.tv/show/{0}";

        public static readonly string MovieDB_Images = @"https://image.tmdb.org/t/p/original{0}";
    }

    public struct GroupLabelStyle
    {
        public static readonly string EpCount = "Total Episode Count";
        public static readonly string Unwatched = "Only Unwatched Episode Count";
        public static readonly string WatchedUnwatched = "Watched and Unwatched Episode Counts";
    }

    public struct EpisodeLabelStyle
    {
        public static readonly string IconsDate = "Icons and Date";
        public static readonly string IconsOnly = "Icons Only";
    }

    #endregion

    public struct WebURLStrings
    {
    }

    public struct EpisodeTypeStrings
    {
        public static readonly string Normal = Resources.EpisodeType_Episodes;
        public static readonly string Credits = Resources.EpisodeType_Credits;
        public static readonly string Specials = Resources.EpisodeType_Specials;
        public static readonly string Trailer = Resources.EpisodeType_Trailer;
        public static readonly string Parody = Resources.EpisodeType_Parody;
        public static readonly string Other = Resources.EpisodeType_Other;
    }

    public struct TvDB
    {
        public static readonly string apiKey = "B178B8940CAF4A2C";
    }
}
