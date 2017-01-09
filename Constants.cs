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
    }
}
