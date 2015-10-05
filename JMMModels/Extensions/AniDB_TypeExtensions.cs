using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMModels.Extensions
{
    public static class AniDB_TypeExtensions
    {
        public static string ToText(this Childs.AniDB_Type tp)
        {
            switch (tp)
            {
                case Childs.AniDB_Type.Movie: return "Movie";
                case Childs.AniDB_Type.OVA: return "OVA";
                case Childs.AniDB_Type.TvSeries: return "TV Series";
                case Childs.AniDB_Type.TvSpecial: return "TV Special";
                case Childs.AniDB_Type.Web: return "Web";
                default: return "Other";
            }

        }
    }
}
