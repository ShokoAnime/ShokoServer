namespace Shoko.Models
{
    public class Constants
    {
        public struct LinkFlags
        {
            public static readonly int FlagLinkTvDB = 1;
            public static readonly int FlagLinkTrakt = 2;
            public static readonly int FlagLinkMAL = 4;
            public static readonly int FlagLinkMovieDB = 8;
        }

        public struct Renamer
        {
            public static readonly string TempFileName = "AAA_WORKINGFILE_TEMP_AAA";
        }

        public struct AniDBLanguageType
        {
            public static readonly string Romaji = "X-JAT";
            public static readonly string English = "EN";
            public static readonly string Kanji = "JA";
        }

        public struct AnimeTitleType
        {
            public static readonly string Main = "main";
            public static readonly string Official = "official";
            public static readonly string ShortName = "short";
            public static readonly string Synonym = "synonym";
        }

        public struct MovieDBImageSize
        {
            public static readonly string Original = "original";
            public static readonly string Thumb = "thumb";
            public static readonly string Cover = "cover";
        }

        public struct URLS
        {
            public static readonly string MAL_Series = @"https://myanimelist.net/anime/{0}";
            public static readonly string MAL_SeriesDiscussion = @"https://myanimelist.net/anime/{0}/{1}/forum";

            public static readonly string AniDB_File = @"https://anidb.net/perl-bin/animedb.pl?show=file&fid={0}";
            public static readonly string AniDB_Episode = @"https://anidb.net/perl-bin/animedb.pl?show=ep&eid={0}";
            public static readonly string AniDB_Series = @"https://anidb.net/perl-bin/animedb.pl?show=anime&aid={0}";
            public static readonly string AniDB_SeriesDiscussion = @"https://anidb.net/perl-bin/animedb.pl?show=threads&do=anime&id={0}";
            public static readonly string AniDB_ReleaseGroup = @"https://anidb.net/perl-bin/animedb.pl?show=group&gid={0}";
            public static readonly string AniDB_Images = @"https://img7.anidb.net/pics/anime/{0}";
            public static readonly string AniDB_Series_NewRelease = @"https://anidb.net/perl-bin/animedb.pl?show=addfilem&aid={0}";

            public static readonly string TvDB_Series = @"https://thetvdb.com/?tab=series&id={0}";
            //public static readonly string tvDBEpisodeURLPrefix = @"https://anidb.net/perl-bin/animedb.pl?show=ep&eid={0}";
            public static readonly string TvDB_Images = @"https://thetvdb.com/banners/{0}";

            public static readonly string MovieDB_Series = @"https://www.themoviedb.org/movie/{0}";
            public static readonly string Trakt_Series = @"https://trakt.tv/show/{0}";

            public static readonly string Blank = @"https://blank";


        }

        public struct CharacterType
        {
            public static readonly string MAIN = "main character in";
        }

        public struct DBLogType
        {
            public static readonly string APIAniDBHTTP = "AniDB HTTP";
            public static readonly string APIAniDBUDP = "AniDB UDP";
            public static readonly string APIAzureHTTP = "Cache HTTP";
        }

        // https://wiki.anidb.net/w/WebAOM#Move.2Frename_system



        public struct FileRenameTag_Tag
        {
            public static readonly string AnimeNameRomaji = "%ann";
            public static readonly string AnimeNameKanji = "%kan";
            public static readonly string AnimeNameEnglish = "%eng";
            public static readonly string EpisodeNameRomaji = "%epn";
            //public static readonly string EpisodeNameKanji = "%epk";
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
            public static readonly string OriginalFileName = "%sna"; // The original file name as specified by the sub group
            public static readonly string Censored = "%cen";
            public static readonly string Deprecated = "%dep";


            /*
			%md5 / %MD5	 md5 sum (lower/upper)
			%sha / %SHA	 sha1 sum (lower/upper)
			%inv	 Invalid crc string
			 * */
        }

      

        public struct FileRenameTest_Test
        {
            public static readonly string AnimeID = "A()";
            public static readonly string GroupID = "G()";
            public static readonly string FileVersion = "F()";
            public static readonly string EpisodeNumber = "E()";
            public static readonly string EpisodeCount = "X()";
            public static readonly string RipSource = "R()";
            public static readonly string AnimeType = "T()";
            public static readonly string Year = "Y()";
            public static readonly string DubLanguage = "D()";
            public static readonly string SubLanguage = "S()";
            public static readonly string Codec = "C()";
            public static readonly string Tag = "I()";
            public static readonly string VideoBitDepth = "Z()";
            public static readonly string VideoResolutionWidth = "W()";
            public static readonly string VideoResolutionHeight = "U()";
            public static readonly string ManuallyLinked = "M()";
            public static readonly string HasEpisodes = "N()";
        }

        /*
		public struct StaticGF
		{
			public static readonly int All = -999;

			public static readonly int Predefined = -900;

			public static readonly int Predefined_Years = -901;
			public static readonly int Predefined_Tags = -902;

			public static readonly int Predefined_Years_Child = -921;
			public static readonly int Predefined_Tags_Child = -922;
		}
		*/

    }
}
