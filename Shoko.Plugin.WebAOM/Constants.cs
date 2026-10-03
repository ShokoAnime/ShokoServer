namespace Shoko.Plugin.WebAOM;

/// <summary>
///   Constants used by the WebAOM renamer.
/// </summary>
internal static class Constants
{
    /// <summary>
    ///   Tags that can be used in the rename script.
    /// </summary>
    public readonly struct FileRenameTag
    {
        /// <summary>
        ///   Main anime title (romaji).
        /// </summary>
        public static readonly string AnimeNameMain = "%ann";
        /// <summary>
        ///   Anime title in kanji (Japanese).
        /// </summary>
        public static readonly string AnimeNameKanji = "%kan";
        /// <summary>
        ///   Anime title in English.
        /// </summary>
        public static readonly string AnimeNameEnglish = "%eng";
        /// <summary>
        ///   Episode title in romaji.
        /// </summary>
        public static readonly string EpisodeNameRomaji = "%epr";
        /// <summary>
        ///   Episode title in English.
        /// </summary>
        public static readonly string EpisodeNameEnglish = "%epn";
        /// <summary>
        ///   Episode number.
        /// </summary>
        public static readonly string EpisodeNumber = "%enr";
        /// <summary>
        ///   Release group short name.
        /// </summary>
        public static readonly string GroupShortName = "%grp";
        /// <summary>
        ///   Release group long name.
        /// </summary>
        public static readonly string GroupLongName = "%grl";
        /// <summary>
        ///   ED2K hash in lowercase.
        /// </summary>
        public static readonly string ED2KLower = "%ed2";
        /// <summary>
        ///   ED2K hash in uppercase.
        /// </summary>
        public static readonly string ED2KUpper = "%ED2";
        /// <summary>
        ///   CRC32 checksum in lowercase.
        /// </summary>
        public static readonly string CRCLower = "%crc";
        /// <summary>
        ///   CRC32 checksum in uppercase.
        /// </summary>
        public static readonly string CRCUpper = "%CRC";
        /// <summary>
        ///   File version (e.g. 1, 2, 3 for corrected releases).
        /// </summary>
        public static readonly string FileVersion = "%ver";
        /// <summary>
        ///   Rip source (e.g. Blu-ray, TV, DVD, www).
        /// </summary>
        public static readonly string Source = "%src";
        /// <summary>
        ///   Video resolution as WxH (e.g. 1920x1080).
        /// </summary>
        public static readonly string Resolution = "%res";
        /// <summary>
        ///   Video frame height in pixels.
        /// </summary>
        public static readonly string VideoHeight = "%vdh";
        /// <summary>
        ///   Release year of the anime.
        /// </summary>
        public static readonly string Year = "%yea";
        /// <summary>
        ///   Total number of episodes in the series.
        /// </summary>
        public static readonly string Episodes = "%eps";
        /// <summary>
        ///   Anime type (unknown, TV, OVA, Movie, TV Special, Other, web).
        /// </summary>
        public static readonly string Type = "%typ";
        /// <summary>
        ///   File ID in the AniDB database.
        /// </summary>
        public static readonly string FileID = "%fid";
        /// <summary>
        ///   Anime ID in the AniDB database.
        /// </summary>
        public static readonly string AnimeID = "%aid";
        /// <summary>
        ///   Episode ID in the AniDB database.
        /// </summary>
        public static readonly string EpisodeID = "%eid";
        /// <summary>
        ///   Release group ID in the AniDB database.
        /// </summary>
        public static readonly string GroupID = "%gid";
        /// <summary>
        ///   Dub (audio) language of the first audio track.
        /// </summary>
        public static readonly string DubLanguage = "%dub";
        /// <summary>
        ///   Subtitle language of the first subtitle track.
        /// </summary>
        public static readonly string SubLanguage = "%sub";
        /// <summary>
        ///   Video codec name; multiple tracks separated with '.
        /// </summary>
        public static readonly string VideoCodec = "%vid";
        /// <summary>
        ///   Audio codec name; multiple tracks separated with '.
        /// </summary>
        public static readonly string AudioCodec = "%aud";
        /// <summary>
        ///   Video bit depth (e.g. 8bit, 10bit).
        /// </summary>
        public static readonly string VideoBitDepth = "%bit";

        /// <summary>
        ///   Original filename as specified by the sub group.
        /// </summary>
        public static readonly string OriginalFileName = "%sna";

        /// <summary>
        ///   Censored flag: non-empty if the file is censored.
        /// </summary>
        public static readonly string Censored = "%cen";
        /// <summary>
        ///   Deprecated flag: non-empty if the file is deprecated/corrupted.
        /// </summary>
        public static readonly string Deprecated = "%dep";

        /*
        %md5 / %MD5	 md5 sum (lower/upper)
        %sha / %SHA	 sha1 sum (lower/upper)
        %inv	 Invalid crc string
         * */
    }

    /// <summary>
    ///   Reserved words for the rename script.
    /// </summary>
    public readonly struct FileRenameReserved
    {
        /// <summary>
        ///   Keyword indicating the action should always be applied, with no test.
        /// </summary>
        public static readonly string Do = "DO";
        /// <summary>
        ///   Keyword that causes the rename script to abort and skip renaming.
        /// </summary>
        public static readonly string Fail = "FAIL";
        /// <summary>
        ///   Keyword to append tag-expanded text to the filename.
        /// </summary>
        public static readonly string Add = "ADD";
        /// <summary>
        ///   Keyword to perform a find-and-replace on the current filename.
        /// </summary>
        public static readonly string Replace = "REPLACE";
        /// <summary>
        ///   Value used for files with no audio or subtitle tracks.
        /// </summary>
        public static readonly string None = "none";
        /// <summary>
        ///   Value used when the type or source is unknown.
        /// </summary>
        public static readonly string Unknown = "unknown";
    }
}
