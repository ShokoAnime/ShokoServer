using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Models
{
    public class Constants
    {
        public static readonly int FlagLinkTvDB = 1;
        public static readonly int FlagLinkTrakt = 2;
        public static readonly int FlagLinkMAL = 4;
        public static readonly int FlagLinkMovieDB = 8;

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
            public static readonly string MAL_Series = @"http://myanimelist.net/anime/{0}";
            public static readonly string MAL_SeriesDiscussion = @"http://myanimelist.net/anime/{0}/{1}/forum";

            public static readonly string AniDB_File = @"http://anidb.net/perl-bin/animedb.pl?show=file&fid={0}";
            public static readonly string AniDB_Episode = @"http://anidb.net/perl-bin/animedb.pl?show=ep&eid={0}";
            public static readonly string AniDB_Series = @"http://anidb.net/perl-bin/animedb.pl?show=anime&aid={0}";
            public static readonly string AniDB_SeriesDiscussion = @"http://anidb.net/perl-bin/animedb.pl?show=threads&do=anime&id={0}";
            public static readonly string AniDB_ReleaseGroup = @"http://anidb.net/perl-bin/animedb.pl?show=group&gid={0}";
            public static readonly string AniDB_Images = @"http://img7.anidb.net/pics/anime/{0}";
            public static readonly string AniDB_Series_NewRelease = @"http://anidb.net/perl-bin/animedb.pl?show=addfilem&aid={0}";

            public static readonly string TvDB_Series = @"http://thetvdb.com/?tab=series&id={0}";
            //public static readonly string tvDBEpisodeURLPrefix = @"http://anidb.net/perl-bin/animedb.pl?show=ep&eid={0}";
            public static readonly string TvDB_Images = @"http://thetvdb.com/banners/{0}";

            public static readonly string MovieDB_Series = @"http://www.themoviedb.org/movie/{0}";
            public static readonly string Trakt_Series = @"http://trakt.tv/show/{0}";

            public static readonly string Blank = @"http://blank";


        }
    }
}
