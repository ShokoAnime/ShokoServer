using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.Server;

namespace Shoko.Commons.Extensions
{
    public static class AniDB_EpisodeExtensions
    {
        public static string GetAirDateFormatted(this AniDB_Episode episode)
        {
            try
            {
                return AniDB.GetAniDBDate(episode.AirDate);
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static DateTime? GetAirDateAsDate(this AniDB_Episode episode)
        {
            return AniDB.GetAniDBDateAsDate(episode.AirDate);
        }

        public static bool GetFutureDated(this AniDB_Episode episode)
        {
            if (!episode.GetAirDateAsDate().HasValue) return true;

            return episode.GetAirDateAsDate().Value > DateTime.Now;
        }

        public static enEpisodeType GetEpisodeTypeEnum(this AniDB_Episode episode)
        {
            return (enEpisodeType) episode.EpisodeType;
        }

 
    }
}
