using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMModels.Childs;

namespace JMMModels.Extensions
{
    public static class AniDB_AnimeExtensions
    {

        public static bool IsTvDBLinkDisabled(this AniDB_Anime anime)
        {
            return (anime.DisableExternalLinksFlag & LinkFlags.LinkTvDB) > 0;
        }

        public static bool IsTraktLinkDisabled(this AniDB_Anime anime)
        {
            return (anime.DisableExternalLinksFlag & LinkFlags.LinkTrakt) > 0;
        }

        public static bool IsMALLinkDisabled(this AniDB_Anime anime)
        {
            return (anime.DisableExternalLinksFlag & LinkFlags.LinkMAL) > 0;
        }

        public static bool IsMovieDBLinkDisabled(this AniDB_Anime anime)
        {
            return (anime.DisableExternalLinksFlag & LinkFlags.LinkMovieDB) > 0;
        }
    }
}
