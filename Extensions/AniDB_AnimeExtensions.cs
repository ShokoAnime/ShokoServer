using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Commons.Utils;
using Shoko.Models;
using Shoko.Models.Server;

namespace Shoko.Commons.Extensions
{
    public static class AniDB_AnimeExtensions
    {
        public static enAnimeType GetAnimeTypeEnum(this AniDB_Anime anime)
        {
            if (anime.AnimeType > 5) return enAnimeType.Other;
            return (enAnimeType) anime.AnimeType;
        }

        public static bool GetFinishedAiring(this AniDB_Anime anime)
        {
            if (!anime.EndDate.HasValue) return false; // ongoing

            // all series have finished airing 
            if (anime.EndDate.Value < DateTime.Now) return true;

            return false;
        }

        public static bool GetIsTvDBLinkDisabled(this AniDB_Anime anime)
        {
            return (anime.DisableExternalLinksFlag & Constants.FlagLinkTvDB) > 0;
        }

        public static bool GetIsTraktLinkDisabled(this AniDB_Anime anime)
        {
            return (anime.DisableExternalLinksFlag & Constants.FlagLinkTrakt) > 0;
        }

        public static bool GetIsMALLinkDisabled(this AniDB_Anime anime)
        {
            return (anime.DisableExternalLinksFlag & Constants.FlagLinkMAL) > 0;
        }

        public static bool GetIsMovieDBLinkDisabled(this AniDB_Anime anime)
        {
            return (anime.DisableExternalLinksFlag & Constants.FlagLinkMovieDB) > 0;
        }

        public static int GetAirDateAsSeconds(this AniDB_Anime anime)
        {
            return AniDB.GetAniDBDateAsSeconds(anime.AirDate);
        }

        public static string GetAirDateFormatted(this AniDB_Anime anime)
        {
            return AniDB.GetAniDBDate(anime.GetAirDateAsSeconds());
        }

        public static void SetAnimeTypeRAW(this AniDB_Anime anime, string value)
        {
            anime.AnimeType = (int) RawToType(value);
        }

        public static string GetAnimeTypeRAW(this AniDB_Anime anime)
        {
            return ConvertToRAW((AnimeTypes) anime.AnimeType);
        }
        public static AnimeTypes RawToType(string raw)
        {
            switch (raw.ToLowerInvariant().Trim())
            {
                case "movie":
                    return AnimeTypes.Movie;
                case "ova":
                    return AnimeTypes.OVA;
                case "tv series":
                    return AnimeTypes.TV_Series;
                case "tv special":
                    return AnimeTypes.TV_Special;
                case "web":
                    return AnimeTypes.Web;
                default:
                    return AnimeTypes.Other;
            }
        }

        public static string ConvertToRAW(AnimeTypes t)
        {
            switch (t)
            {
                case AnimeTypes.Movie:
                    return "movie";
                case AnimeTypes.OVA:
                    return "ova";
                case AnimeTypes.TV_Series:
                    return "tv series";
                case AnimeTypes.TV_Special:
                    return "tv special";
                case AnimeTypes.Web:
                    return "web";
                default:
                    return "other";
            }
        }

        public static string GetAnimeTypeName(this AniDB_Anime anime)
        {
            return Enum.GetName(typeof(AnimeTypes), (AnimeTypes) anime.AnimeType).Replace('_', ' ');
        }

        public static HashSet<string> GetAllTags(this AniDB_Anime anime)
        {
            return new HashSet<string>(
             anime.AllTags.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                 .Select(a => a.Trim())
                 .Where(a => !string.IsNullOrEmpty(a)), StringComparer.InvariantCultureIgnoreCase);         
        }

        public static HashSet<string> GetAllTitles(this AniDB_Anime anime)
        {
            return new HashSet<string>(
                 anime.AllTitles.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                     .Select(a => a.Trim())
                     .Where(a => !string.IsNullOrEmpty(a)), StringComparer.InvariantCultureIgnoreCase);
        }


        public static bool GetSearchOnTvDB(this AniDB_Anime anime)
        {
            return anime.AnimeType != (int) AnimeTypes.Movie && !(anime.Restricted > 0);
        }

        public static bool GetSearchOnMovieDB(this AniDB_Anime anime)
        {
            return anime.AnimeType == (int) AnimeTypes.Movie && !(anime.Restricted > 0);
        }

        public static decimal GetAniDBRating(this AniDB_Anime anime)
        {
            if (anime.GetAniDBTotalVotes() == 0)
                return 0;
            return anime.GetAniDBTotalRating() / (decimal) anime.GetAniDBTotalVotes();
        }

        public static decimal GetAniDBTotalRating(this AniDB_Anime anime)
        {
            decimal totalRating = 0;
            totalRating += (decimal) anime.Rating * anime.VoteCount;
            totalRating += (decimal) anime.TempRating * anime.TempVoteCount;
            return totalRating;
        }

        public static int GetAniDBTotalVotes(this AniDB_Anime anime)
        {
            return anime.TempVoteCount + anime.VoteCount;
        }

        public static string ToStringEx(this AniDB_Anime anime)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("AnimeID: " + anime.AnimeID);
            sb.Append(" | Main Title: " + anime.MainTitle);
            sb.Append(" | EpisodeCount: " + anime.EpisodeCount);
            sb.Append(" | AirDate: " + anime.AirDate);
            sb.Append(" | Picname: " + anime.Picname);
            sb.Append(" | Type: " + anime.GetAnimeTypeRAW());
            return sb.ToString();
        }

    }
}
