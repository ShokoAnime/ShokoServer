using JMMModels.ClientExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMModels.Extensions
{
    public static class AnimeSerieExtensions
    {
        public static bool IsComplete(this JMMModels.AnimeSerie s)
        {
            if (s.AniDB_Anime.EndDate != null && s.AniDB_Anime.EndDate.Date.ToDateTime()<DateTime.Now && s.MissingEpisodeCount==0 && s.MissingEpisodeCountGroups==0)
                return true;
            return false;
        }

        public static bool HasFinishedAiring(this JMMModels.AnimeSerie s)
        {
            if (s.AniDB_Anime.EndDate != null && s.AniDB_Anime.EndDate.Date.ToDateTime() < DateTime.Now)
                return true;
            return false;
        }

        public static bool IsCurrentlyAiring(this JMMModels.AnimeSerie s)
        {
            if (s.AniDB_Anime.EndDate == null || s.AniDB_Anime.EndDate.Date.ToDateTime()>DateTime.Now)
                return true;
            return false;
        }

        public static bool HasTvDB(this JMMModels.AnimeSerie s)
        {
            return (s.AniDB_Anime.TvDBs.Count > 0);
        }

        public static bool HasMAL(this JMMModels.AnimeSerie s)
        {
            return (s.AniDB_Anime.MALs.Count > 0);
        }

        public static bool HasTrakt(this JMMModels.AnimeSerie s)
        {
            return (s.AniDB_Anime.Trakts.Count > 0);
        }
        public static bool HasMovieDB(this JMMModels.AnimeSerie s)
        {
            return (s.AniDB_Anime.MovieDBs.Count > 0);
        }
        public static bool HasAudioLanguage(this JMMModels.AnimeSerie s, string lang)
        {
            lang = lang.ToLowerInvariant();
            return s.Languages.Any(a => a.ToLowerInvariant() == lang);
        }
        public static bool HasSubtitleLanguage(this JMMModels.AnimeSerie s, string lang)
        {
            lang = lang.ToLowerInvariant();
            return s.Subtitles.Any(a => a.ToLowerInvariant() == lang);
        }

        public static bool HasVideoQuality(this JMMModels.AnimeSerie s, string quality)
        {
            quality = quality.ToLowerInvariant();
            return s.AvailableVideoQualities.Any(a => a.ToLowerInvariant() == quality);

        }
        public static bool HasReleaseQuality(this JMMModels.AnimeSerie s, string quality)
        {
            quality = quality.ToLowerInvariant();
            return s.AvailableReleaseQualities.Any(a => a.ToLowerInvariant() == quality);

        }
    }
}
