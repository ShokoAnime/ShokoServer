using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMModels.Childs;

namespace JMMDatabase.Extensions
{
    public static class AniDB_AnimeExtensions
    {
        public static string ToString(this AniDB_Anime a)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("AnimeID: " + a.Id);
            sb.Append(" | Main Title: " + a.MainTitle);
            sb.Append(" | EpisodeCount: " + a.EpisodeCount);
            sb.Append(" | AirDate: " + a.AirDate);
            sb.Append(" | Picname: " + a.Picname);
            sb.Append(" | Type: " + a.AnimeType);
            return sb.ToString();
        }
    }
}
